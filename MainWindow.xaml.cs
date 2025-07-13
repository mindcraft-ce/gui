using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices; // For DllImport
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops; // For ISystemBackdropControllerWithAcrylicBackground
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
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

        public MainWindow()
        {
            InitializeComponent();
            this.Title = "mindcraft-ce";
            TrySetSystemBackdrop(SystemBackdropType.Mica);
        }

        private void nvSample_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {

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
                    System.Diagnostics.Debug.WriteLine("Mica backdrop applied successfully.");
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
                    System.Diagnostics.Debug.WriteLine("Desktop Acrylic backdrop applied successfully.");
                }
                else { System.Diagnostics.Debug.WriteLine("Desktop Acrylic backdrop is not supported on this system."); }
            }
            else // DefaultColor or if others not supported
            {
                // Clear any existing backdrop
                ClearSystemBackdrop();
                System.Diagnostics.Debug.WriteLine("Default color backdrop applied (or previous backdrop cleared).");
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

        private void Window_Closed_Backdrop(object sender, WindowEventArgs args) // Renamed from Window_Closed
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
            System.Diagnostics.Debug.WriteLine("Backdrop resources cleaned up.");
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
