using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public PlayView()
        {
            InitializeComponent();
            DataContext = ViewModel;
            updateAgentCount();
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle the play button click event
            // Start the actual game with `node main.js` when ready.
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
                updateAgentCount();
            }
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
                ViewModel.RemoveAgent(agent);
                updateAgentCount();
            }
        }
    }
}
