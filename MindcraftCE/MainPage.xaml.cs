using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices; // For DllImport
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.UI.Composition.SystemBackdrops; // For ISystemBackdropControllerWithAcrylicBackground
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using MindcraftCE.Views;
using Newtonsoft.Json.Linq;
using Windows.Media;
using Windows.Media.Playback;
using WinRT; // For As Guid Attribute
using WinRT.Interop;

namespace MindcraftCE
{
    public sealed partial class MainPage : Page
    {
        public Frame RootFrame
        {
            get { return contentFrame; }
        }

        public void UpdateNavigationView()
        {
            var installed = UpdatesView.GetMetadataSync()["installed"]?.Value<bool>() == true;
            if (installed)
            {
                foreach (var item in nvSample.MenuItems)
                {
                    if (item is NavigationViewItem nvi && nvi.IsEnabled == false)
                    {
                        nvi.IsEnabled = true; // Enable all items if installed
                    }
                }
            } else
            {
                foreach (var item in nvSample.MenuItems)
                {
                    if (item is NavigationViewItem nvi && nvi.IsEnabled == true && (new List<string> { "Agents", "Settings", "API Keys" }).Contains(nvi.Content))
                    {
                        nvi.IsEnabled = false; // Disable items if not installed
                    }
                }
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            agentDisplayImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/minecraft.png"));

            contentFrame.Navigate(typeof(PlayView));
            nvSample.SelectedItem = nvSample.MenuItems.SingleOrDefault(item => item is NavigationViewItem nvi && nvi.Tag?.ToString() == "MindcraftCE.Views.PlayView");
            
            UpdateAgentDisplay(0, null, null);

            var installed = UpdatesView.GetMetadataSync()["installed"]?.Value<bool>() == true;
            if (installed)
            {
                foreach (var item in nvSample.MenuItems)
                {
                    if (item is NavigationViewItem nvi && nvi.IsEnabled == false)
                    {
                        nvi.IsEnabled = true; // Enable all items if installed
                    }
                }
            }
        }

        private void nvSample_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                // Get the page type from the Tag property
                var container = args.InvokedItemContainer;
                if (container.Tag == null) return;
                var navItemTag = container.Tag.ToString();
                if (string.IsNullOrEmpty(navItemTag)) return;

                Type pageType = Type.GetType(navItemTag);
                if (pageType != null && contentFrame.CurrentSourcePageType != pageType)
                {
                    contentFrame.Navigate(pageType);
                }
            }
        }

        private void agentDisplay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Navigate to the PlayView
            // contentFrame.Navigate(typeof(PlayView));
        }

        public void UpdateAgentDisplay(int agentsOnline, string? host, string? port)
        {
            if (agentsOnline > 0)
            {
                if (agentsOnline > 1)
                {
                    agentDisplayCount.Text = $"{agentsOnline.ToString()} agents online";
                }
                else
                {
                    agentDisplayCount.Text = "1 agent online.";
                }
                if (port == "25565")
                {
                    agentDisplayStatus.Text = $"Connected to {host}";
                }
                else {
                    agentDisplayStatus.Text = $"Connected to {host}:{port.ToString()}";
                }

            }
            else
            {
                agentDisplayCount.Text = "Offline";
                agentDisplayStatus.Text = "Not connected.";
            }
        }

        private void nvSample_SizeChanged(object sender, object e)
        {
            if (sender is NavigationView navView)
            {
                if (!navView.IsPaneOpen)
                {
                    agentDisplayTextPanel.Visibility = Visibility.Collapsed;

                    agentDisplay.Margin = new Thickness(0, 0, 0, 0);

                    Grid.SetColumnSpan(agentDisplayImage, 2);

                    double newSize = navView.CompactPaneLength - 24;
                    // Console.WriteLine($"CompactPaneLength: {navView.CompactPaneLength}, New Size: {newSize}");
                    agentDisplayImage.Width = newSize;
                    agentDisplayImage.Height = newSize; // Set height to maintain aspect ratio
                }
                else
                {
                    agentDisplay.Margin = new Thickness(0, 8, 0, 8);
                    agentDisplayTextPanel.Visibility = Visibility.Visible;

                    Grid.SetColumnSpan(agentDisplayImage, 1);

                    agentDisplayImage.Width = 40;
                    agentDisplayImage.Height = 40;
                }
            }
        }
    }
}
