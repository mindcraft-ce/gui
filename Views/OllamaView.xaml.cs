using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using mindcraft_ce.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OllamaView : Page, INotifyPropertyChanged
    {
        public ObservableCollection<OllamaModel> Models { get; } = new();

        private OllamaModel _selectedModel;
        public OllamaModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = value;
                OnPropertyChanged(nameof(SelectedModel));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public OllamaView()
        {
            InitializeComponent();
            this.DataContext = this;
            this.PropertyChanged += OllamaView_PropertyChanged;
        }

        private void OllamaView_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedModel))
            {
                UpdateManualModelDetailsUI();
            }
        }

        private void UpdateManualModelDetailsUI()
        {
            if (SelectedModel == null)
                return;

            panelTitle.Text = SelectedModel.Name;
            chatMessages.Children.Clear();
            sendButton.IsEnabled = true;
            inputTextBox.IsEnabled = true;
        }

        private void inputTextBox_KeyDown(object sender, KeyRoutedEventArgs e) {
            // check if the Enter key was pressed
            if (e.Key == Windows.System.VirtualKey.Enter && !e.KeyStatus.IsMenuKeyDown)
            {
                e.Handled = true; // prevents the default behavior of the Enter key
                SendMessage();
            }
        }
        private void sendButton_Click(object sender, RoutedEventArgs e) { SendMessage(); }

        private void SendChatMessage(string role, string content)
        {
            chatMessages.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock text = new TextBlock
            {
                Text = content,
            };
            Border message = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderBrush = (Brush)Application.Current.Resources["AppBarBackgroundThemeBrush"],
                BorderThickness = new Thickness(4),
                Margin = new Thickness(5, 5, 0, 5),
                Padding = new Thickness(5),
                HorizontalAlignment = role == "model" ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Width = double.NaN, // Auto width
            };
            message.Child = text;
            Grid.SetRow(message, chatMessages.RowDefinitions.Count - 1);
            chatMessages.Children.Add(message);
            inputTextBox.Text = string.Empty; // Clear input after sending
        }

        private List<Dictionary<string, string>> GetChatMessages()
        {
            List<Dictionary<string, string>> messages = new List<Dictionary<string, string>>();
            foreach (var child in chatMessages.Children)
            {
                if (child is Border border && border.Child is TextBlock textBlock)
                {
                    Dictionary<string, string> message = new Dictionary<string, string>
                    {
                        { "role", border.HorizontalAlignment == HorizontalAlignment.Left ? "model" : "user" },
                        { "content", textBlock.Text }
                    };
                    messages.Add(message);
                }
            }
            return messages;
        }

        private async void SendMessage()
        {
            string userMessage = inputTextBox.Text.Trim();
            string response = "";
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return; // Do not send empty messages
            }
            SendChatMessage("user", userMessage);

            sendButton.IsEnabled = false;
            inputTextBox.IsEnabled = false;

            SendChatMessage("model", "Loading...");

            JObject payload = new JObject
            {
                ["model"] = SelectedModel.Name,
                ["stream"] = false,
                ["messages"] = JArray.FromObject(GetChatMessages()),
            };
            // Send the payload to the Ollama API
            var httpResponse = await client.PostAsync(baseUrl + "/api/chat", 
                new StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json"));
            var content = await httpResponse.Content.ReadAsStringAsync();
            JObject res = JObject.Parse(content);
            if (httpResponse.IsSuccessStatusCode && res != null)
            {
                response = res["message"]?["content"]?.ToString() ?? "No response from model.";
            }
            else
            {
                response = "Error: " + (res?["error"]?.ToString() ?? "Unknown error");
            }

            chatMessages.Children.RemoveAt(chatMessages.Children.Count - 1); // Remove the "Loading..." message

            SendChatMessage("model", response);

            sendButton.IsEnabled = true;
            inputTextBox.IsEnabled = true;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CheckOllama();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            PropertyChanged = null; // prevent lingering references and code 3221225477 (0xc0000005) 'Access violation'
        }

        private readonly HttpClient client = new HttpClient();
        private readonly string baseUrl = "http://localhost:11434";

        private async void CheckOllama()
        {

            // Check if Ollama is installed
            try
            {
                var response = await client.GetAsync(baseUrl + "/api/version");
                var content = await response.Content.ReadAsStringAsync();
                JObject res = JObject.Parse(content);

                if (res != null)
                {
                    string version = res["version"]?.ToString() ?? "unknown";
                    messageBox.Text += $"\nOllama API is online.\nVersion {version}";
                }
                else
                {
                    messageBox.Text += $"\nError: {content}";
                }
            }
            catch (HttpRequestException)
            {
                messageBox.Text += "\nOllama API is offline.";
                return;
            }
            catch (Exception ex)
            {
                messageBox.Text += $"\nError: {ex.Message}";
                Debug.WriteLine("Error checking Ollama API: " + ex);
                return;
            }

            try
            {
                var modelsResponse = await client.GetAsync(baseUrl + "/api/tags");
                var modelsContent = await modelsResponse.Content.ReadAsStringAsync();
                var modelsData = JObject.Parse(modelsContent);
                var modelsArray = modelsData["models"] as JArray;
                
                static string FormatSize(long sizeInBytes)
                {
                    const long OneMB = 1024 * 1024;
                    const long OneGB = 1024 * OneMB;

                    if (sizeInBytes >= OneGB)
                    {
                        double gb = (double)sizeInBytes / OneGB;
                        return $"{gb:F2} GB";
                    }
                    else
                    {
                        double mb = (double)sizeInBytes / OneMB;
                        return $"{mb:F2} MB";
                    }
                }

                foreach (var model in modelsArray)
                {
                    OllamaModel modelItem = new OllamaModel {
                        Name                = model["name"]?.ToString(),
                        Size                = FormatSize(model["size"]?.Value<int>() ?? 0),
                        ParameterSize       = model["details"]["parameter_size"]?.ToString(),
                        QuantizationLevel   = model["details"]["quantization_level"]?.ToString()
                    };
                    Models.Add(modelItem);
                }
            }
            catch (Exception ex)
            { 
                Debug.WriteLine("Error fetching Ollama models: " + ex.Message);
            }
        }
    }

    public class OllamaModel
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string ParameterSize { get; set; }
        public string QuantizationLevel { get; set; }
    }
}