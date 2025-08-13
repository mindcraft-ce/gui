using System;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Uno.Resizetizer;

namespace mindcraft_ce;

public partial class App : Application
{
    /// <summary>
    /// We are replacing the template's "Window" with our own "MainWindow".
    /// This property holds the reference to it, replacing your old static property.
    /// </summary>
    protected MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        
        // Your global exception handler has been moved here.
        this.UnhandledException += App_UnhandledException;
    }

    /// <summary>
    /// Your original unhandled exception handler.
    /// It's modified slightly to use the new `MainWindow` property instead of the old static one.
    /// </summary>
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Mark the exception as handled so the app doesn't crash immediately.
        e.Handled = true;

        // Log the full exception details. You can also use the new logger below.
        System.Diagnostics.Debug.WriteLine("====================================================");
        System.Diagnostics.Debug.WriteLine("GLOBAL UNHANDLED EXCEPTION CAUGHT");
        System.Diagnostics.Debug.WriteLine($"Exception: {e.Exception}");
        System.Diagnostics.Debug.WriteLine("====================================================");

        // Show a user-friendly dialog.
        if (MainWindow != null)
        {
            _ = new ContentDialog
            {
                Title = "An Unexpected Error Occurred",
                Content = $"The application will now close.\n\nPlease report this error:\n{e.Message}",
                CloseButtonText = "OK",
                XamlRoot = MainWindow.Content.XamlRoot // Use the instance property here
            }.ShowAsync();
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// We have replaced the template's Frame navigation with your app's direct MainWindow creation.
    /// </summary>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Instead of creating a generic Window, we create an instance of your MainWindow.
        MainWindow = new MainWindow();

#if DEBUG
        // This enables Hot Reload. Keep this.
        MainWindow.UseStudio();
#endif
        
        // This is a new helper from the template to set the window icon. Keep this.
        MainWindow.SetWindowIcon();

        // This is the same as your old code.
        MainWindow.Activate();
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails.
    /// While we are not using Frame navigation right now, it's good practice to keep this.
    /// </summary>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging. This is a powerful feature from the template.
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());
            builder.AddConsole();
#else
            builder.AddConsole();
#endif
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
