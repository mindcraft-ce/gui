using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
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
    public sealed partial class KeysView : Page
    {
        public KeysView()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateLayout();
        }

        private async void UpdateLayout() {
            var installationPath = (await UpdatesView.GetMetadata())["installation_path"]?.Value<String>();
            var keys_json = Path.Combine(installationPath, "keys.json");
            var contents = JObject.Parse(await FileIO.ReadTextAsync(await StorageFile.GetFileFromPathAsync(keys_json)));

            KeysGrid.Children.Clear();

            KeysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            KeysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int i = 0;
            foreach(var prop in contents.Properties())
            {
                KeysGrid.RowDefinitions.Add(new RowDefinition
                {
                    Height = GridLength.Auto,
                });

                TextBlock label = new TextBlock
                {
                    Text = prop.Name.Replace("_", " "),
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(label, 0);
                Grid.SetRow(label, i);

                TextBox value = new TextBox
                {
                    Text = prop.Value.ToString(),
                    PlaceholderText = "Not Set",
                    Margin = new Thickness(5),
                };
                Grid.SetColumn(value, 1);
                Grid.SetRow(value, i);

                KeysGrid.Children.Add(label);
                KeysGrid.Children.Add(value);

                i++;
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            JObject keysJson = new JObject();
            foreach (var child in KeysGrid.Children)
            {
                if (child is TextBox textBox)
                {
                    int row = Grid.GetRow(textBox);
                    int column = Grid.GetColumn(textBox);
                    if (column == 1) // Only save values from the second column
                    {
                        string keyName = ((TextBlock)KeysGrid.Children[row * 2]).Text.Replace(" ", "_").ToUpper();
                        keysJson[keyName] = textBox.Text;
                    }
                }
            }

            var installationPath = UpdatesView.GetMetadataSync()["installation_path"]?.Value<string>();

            if (installationPath == null)
                return;

            var keysFilePath = Path.Combine(installationPath, "keys.json");
            // 4.
            try
            {
                using (var fileStream = new FileStream(keysFilePath, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.Write(keysJson.ToString());
                }
                ContentDialog saveDialog = new ContentDialog
                {
                    Title = "Keys Saved",
                    Content = "Your API keys have been saved successfully.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                };
                saveDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // Handle exceptions, e.g., show an error message to the user
                System.Diagnostics.Debug.WriteLine($"Error saving keys: {ex.Message}");
            }
        }
    }
}
