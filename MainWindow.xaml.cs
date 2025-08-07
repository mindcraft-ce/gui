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
using mindcraft_ce.Views;
using Newtonsoft.Json.Linq;
using Windows.Media;
using Windows.Media.Playback;
using WinRT; // For As Guid Attribute
using WinRT.Interop;

namespace mindcraft_ce
{
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper? m_wsdqHelper; // See below for implementation.
        MicaController? m_micaController;
        DesktopAcrylicController? m_acrylicController;
        SystemBackdropConfiguration? m_configurationSource;

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

        public MainWindow()
        {
            InitializeComponent();
            this.Title = "mindcraft-ce";
            TrySetSystemBackdrop(SystemBackdropType.Mica);
            agentDisplayImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/minecraft.png"));

            contentFrame.Navigate(typeof(PlayView));
            nvSample.SelectedItem = nvSample.MenuItems.SingleOrDefault(item => item is NavigationViewItem nvi && nvi.Tag?.ToString() == "mindcraft_ce.Views.PlayView");
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            string iconPath = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets/icon.ico");
            appWindow.SetIcon(iconPath);

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
                    // Debug.WriteLine($"CompactPaneLength: {navView.CompactPaneLength}, New Size: {newSize}");
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

        public enum SystemBackdropType { Mica, DesktopAcrylic, DefaultColor }

        public bool TrySetSystemBackdrop(SystemBackdropType type)
        {
            bool isApplied = false;
            if (type == SystemBackdropType.Mica)
            {
                if (MicaController.IsSupported())
                {
                    m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                    m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                    // Hooking up the policy object
                    m_configurationSource = new SystemBackdropConfiguration();
                    this.Activated += Window_Activated;
                    this.Closed += Window_Closed_Backdrop; // Use a different name if you already have Window_Closed
                    ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                    // Initial configuration state.
                    m_configurationSource.IsInputActive = true;
                    SetConfigurationSourceTheme();

                    m_micaController = new MicaController();
                    m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                    isApplied = true;
                    // System.Diagnostics.Debug.WriteLine("Mica backdrop applied successfully.");
                }
                else { System.Diagnostics.Debug.WriteLine("Mica backdrop is not supported on this system."); }
            }
            else if (type == SystemBackdropType.DesktopAcrylic)
            {
                if (DesktopAcrylicController.IsSupported())
                {
                    m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                    m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                    m_configurationSource = new SystemBackdropConfiguration();
                    this.Activated += Window_Activated;
                    this.Closed += Window_Closed_Backdrop;
                    ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                    m_configurationSource.IsInputActive = true;
                    SetConfigurationSourceTheme();

                    m_acrylicController = new DesktopAcrylicController();
                    m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
                    isApplied = true;
                    // System.Diagnostics.Debug.WriteLine("Desktop Acrylic backdrop applied successfully.");
                }
                else { System.Diagnostics.Debug.WriteLine("Desktop Acrylic backdrop is not supported on this system."); }
            }
            else // DefaultColor or if others not supported
            {
                // Clear any existing backdrop
                ClearSystemBackdrop();
                // System.Diagnostics.Debug.WriteLine("Default color backdrop applied (or previous backdrop cleared).");
                isApplied = true; // Considered applied as it's the fallback
            }
            return isApplied;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (m_configurationSource != null)
            {
                m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
        }

        private void Window_Closed_Backdrop(object sender, WindowEventArgs args)
        {
            ClearSystemBackdrop();

            // Unsubscribe events
            this.Activated -= Window_Activated;
            if (m_configurationSource != null) // Check if it was ever initialized
            {
                m_configurationSource = null;
            }
            if (this.Content is FrameworkElement contentElement)
            {
                contentElement.ActualThemeChanged -= Window_ThemeChanged;
            }
            // System.Diagnostics.Debug.WriteLine("Backdrop resources cleaned up.");
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (m_configurationSource != null && this.Content is FrameworkElement contentElement)
            {
                switch (contentElement.ActualTheme)
                {
                    case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
                }
            }
        }

        private void ClearSystemBackdrop()
        {
            // Dispose of the controllers to clear the backdrop.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            public int dwSize;
            public int threadType;
            public int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

        object? m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTATYPE_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }

}
