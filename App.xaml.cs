using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow? MainWindowInstance { get; private set; }


        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Mark the exception as handled so the app doesn't crash immediately.
            e.Handled = true;

            // Log the full exception details, including the stack trace.
            // This is the most important part.
            System.Diagnostics.Debug.WriteLine("====================================================");
            System.Diagnostics.Debug.WriteLine("GLOBAL UNHANDLED EXCEPTION CAUGHT");
            System.Diagnostics.Debug.WriteLine($"Exception: {e.Exception}");
            System.Diagnostics.Debug.WriteLine("====================================================");

            // OPTIONAL: Show a user-friendly dialog.
            // You need a reference to the main window to do this.
            var window = MainWindowInstance;
            if (window != null)
            {
                new ContentDialog
                {
                    Title = "An Unexpected Error Occurred",
                    Content = $"The application will now close.\n\nPlease report this error:\n{e.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = window.Content.XamlRoot
                }.ShowAsync();
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindowInstance = new MainWindow();
            MainWindowInstance.Activate();
        }
    }
}
