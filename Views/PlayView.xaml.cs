using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using mindcraft_ce.Models;
using mindcraft_ce.ViewModels;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayView : Page, INotifyPropertyChanged
    {
        public AgentViewModel ViewModel { get; } = new();

        private Process? _mindcraftProcess;
        private bool _isErrorHandlingInProgress = false;

        public PlayView()
        {
            InitializeComponent();
            DataContext = ViewModel;
            updateAgentCount();
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void LogMessage(string message) {
            Debug.WriteLine(message);
            DispatcherQueue.TryEnqueue(() => {
                logTextBlock.Text += $"\n{message}";
                logScrollViewer.ChangeView(null, logScrollViewer.ExtentHeight, null);
            });
        }

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            // Copy logs
            string textToCopy = logTextBlock.Text;
            var dataPackage = new DataPackage();
            dataPackage.SetText(textToCopy);
            Clipboard.SetContent(dataPackage);
        }

        private string? getSuggestedFix(string logs, bool force = false, string port = "55916", string mcVersion = "on the correct version")
        {
            string suggestedFix = null;
            if (force) { suggestedFix = "Not sure. Try asking on Discord, or filing a GitHub issue."; }
            string errorMessage = logs;

            if (errorMessage.Contains("ECONNREFUSED"))
            {
                suggestedFix = $"Ensure your game is Open to LAN on port {port}, and you're playing {mcVersion}. If you're using a different version, change it in Settings.";
            }
            else if (errorMessage.Contains("ERR_MODULE_NOT_FOUND"))
            {
                suggestedFix = "A required file is missing. Try reinstalling the app.";
            }
            else if (errorMessage.Contains("ECONNRESET"))
            {
                suggestedFix = $"Make sure that you're playing Minecraft {mcVersion}. If you're using a different version, change it in Settings.";
            }
            else if (errorMessage.Contains("ERR_DLOPEN_FAILED"))
            {
                suggestedFix = "A critical component failed to load. Please try reinstalling the app.";
            }
            else if (errorMessage.Contains("Cannot read properties of null (reading 'version')"))
            {
                suggestedFix = "Try again with a vanilla client - mindcraft-ce doesn't support mods!";
            }
            else if (errorMessage.Contains("not found in keys.json"))
            {
                suggestedFix = "Make sure you've filled in your API keys in the API Keys section.";
            }

            return suggestedFix;
        }

        private async void playButton_Click(object sender, RoutedEventArgs e)
        {
            // If the process is running, the button now acts as a "Stop" button.
            if (_mindcraftProcess != null && !_mindcraftProcess.HasExited)
            {
                LogMessage("--- Stopping process ---");
                try
                {
                    _mindcraftProcess.Kill(); // Forcefully terminate the process
                }
                catch (Exception ex)
                {
                    LogMessage($"Error stopping process: {ex.Message}");
                }
                _isErrorHandlingInProgress = false;
                App.MainWindowInstance.UpdateAgentDisplay(0, null, null);
                // The _mindcraftProcess.Exited event will handle resetting the UI.
                return;
            }

            // --- Start the process ---
            _isErrorHandlingInProgress = false;
            playButton.IsEnabled = false;
            playButton.Content = "Starting...";
            copyButton.Visibility = Visibility.Visible;
            DispatcherQueue.TryEnqueue(() => { logTextBlock.Text = ""; });

            try
            {
                // 1. Get paths and load settings for context
                var metadata = UpdatesView.GetMetadataSync();
                var installationPath = metadata["installation_path"]?.Value<string>();
                if (string.IsNullOrEmpty(installationPath))
                    throw new DirectoryNotFoundException("Installation path not found.");

                var settingsJson = await UpdatesView.GetSettings(); // Use the async version
                var port = settingsJson["port"]?.ToString() ?? "25565"; // Default port
                var mcVersion = settingsJson["minecraft_version"]?.ToString() ?? "the correct version";
                var host = settingsJson["host"]?.ToString() ?? "Unknown";

                var profilesToken = settingsJson["profiles"];
                var agentsOnline = new List<string>();
                if (profilesToken is JArray profilesArray) { agentsOnline = profilesArray.Select(t => t.ToString()).ToList(); }
                int agentCount = agentsOnline.Count;

                // 2. Configure the Process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "node.exe",
                    Arguments = "main.js",
                    WorkingDirectory = installationPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["SETTINGS_PATH"] = Path.Combine(installationPath, "settings.json");

                _mindcraftProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                // 3. Hook up event handlers
                _mindcraftProcess.OutputDataReceived += processDataForErrors;

                _mindcraftProcess.ErrorDataReceived += processDataForErrors;

                void processDataForErrors(object s, DataReceivedEventArgs args)
                {
                    if (string.IsNullOrEmpty(args.Data)) return;

                    // Log the raw error first
                    LogMessage(args.Data);

                    if (_isErrorHandlingInProgress) return;

                    // Now, parse the error for a suggested fix
                    var suggestedFix = getSuggestedFix(args.Data, force: false, port, mcVersion);
                    if (string.IsNullOrEmpty(suggestedFix))
                    {
                        // The error is just a useless piece of stdout then, continue.
                        return;
                    }

                    _isErrorHandlingInProgress = true;

                    LogMessage($"\n\n === ✨ Suggested Fix ✨ === \n{suggestedFix}\n\n");

                    // The error has been identified, now kill the process and show the dialog
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        // Ensure the process is stopped
                        if (_mindcraftProcess != null && !_mindcraftProcess.HasExited)
                        {
                            _mindcraftProcess.Kill();
                        }

                        // Show a helpful dialog
                        
                        var errorDialog = new ContentDialog
                        {
                            Title = "An error occured",
                            Content = suggestedFix,
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        errorDialog.ShowAsync();
                        
                    });
                };

                _mindcraftProcess.Exited += (s, args) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        playButton.Content = "Play";
                        playButton.IsEnabled = true;
                        _mindcraftProcess?.Dispose();
                        _mindcraftProcess = null;
                        _isErrorHandlingInProgress = false;
                        App.MainWindowInstance.UpdateAgentDisplay(0, null, null);
                        LogMessage("--- Process exited ---");
                    });
                };

                // 4. Start the process
                _mindcraftProcess.Start();
                _mindcraftProcess.BeginOutputReadLine();
                _mindcraftProcess.BeginErrorReadLine();

                // Update UI to show it's running
                playButton.IsEnabled = true; // The button is now the "Stop" button
                playButton.Content = "Stop";
                App.MainWindowInstance.UpdateAgentDisplay(agentCount, host, port);
            }
            catch (Exception ex)
            {
                LogMessage($"FATAL ERROR: {ex.Message}");

                playButton.IsEnabled = true;
                playButton.Content = "Play";
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (_mindcraftProcess != null && !_mindcraftProcess.HasExited)
            {
                LogMessage("--- Stopping process ---");
                try
                {
                    _mindcraftProcess.Kill();
                }
                catch (Exception ex)
                {
                    LogMessage($"Error stopping process: {ex.Message}");
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (UpdatesView.GetMetadataSync()["installed"]?.Value<bool>() != true)
            {
                messageBox.Visibility = Visibility.Visible;
                messageBox.Text = "mindcraft-ce is not installed.";
                installButton.Visibility = Visibility.Visible;
                playButton.Visibility = Visibility.Collapsed;
                AgentsTextHeader.Visibility = Visibility.Collapsed;
                AgentViewGrid.Visibility = Visibility.Collapsed;
                return;
            }
            updateAgentCount();
        }

        private void updateAgentCount()
        {
            AgentsTextHeader.Text = "Agents (" + ViewModel.Agents.Count(agent => agent.IsChecked).ToString() + " selected)";
        }

        private void agentListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Agent clickedAgent)
            {
                clickedAgent.IsChecked = !clickedAgent.IsChecked;
                ViewModel.EditAgent(clickedAgent);
            }
            updateAgentCount();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var clickedAgent = (sender as CheckBox).DataContext as Agent;
            clickedAgent.IsChecked = !clickedAgent.IsChecked;
            ViewModel.EditAgent(clickedAgent);
            updateAgentCount();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Agent agent = (sender as Button)?.DataContext as Agent;
            if (agent == null) return;

            // Get reference to MainWindow
            var mainWindow = App.MainWindowInstance;
            if (mainWindow == null) return;

            // Navigate using the MainWindow's Frame
            mainWindow.contentFrame.Navigate(typeof(AgentView), agent);

            // Update selected item in NavigationView
            var navItem = mainWindow.nvSample.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(nvi => nvi.Tag?.ToString() == "mindcraft_ce.Views.AgentView");

            if (navItem != null)
                mainWindow.nvSample.SelectedItem = navItem;
        }



        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Agent agent = (sender as Button)?.DataContext as Agent;
            if (agent == null) return;

            ContentDialog deleteDialog = new ContentDialog
            {
                Title = "Delete Agent",
                Content = $"Are you sure you want to delete the agent '{agent.Name}'?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)Application.Current.Resources["DestructiveButtonStyle"],
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.RemoveAgent(agent);
                updateAgentCount();
            }
        }

        private void installButton_Click(object sender, RoutedEventArgs e)
        {
            // Get reference to MainWindow
            var mainWindow = App.MainWindowInstance;
            if (mainWindow == null) return;

            // Navigate using the MainWindow's Frame
            mainWindow.contentFrame.Navigate(typeof(UpdatesView));

            // Update selected item in NavigationView
            var navItem = mainWindow.nvSample.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(nvi => nvi.Tag?.ToString() == "mindcraft_ce.Views.UpdatesView");

            if (navItem != null)
                mainWindow.nvSample.SelectedItem = navItem;
        }
    }
}
