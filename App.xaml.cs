using System;
using System.Windows;

namespace GtavModManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global unhandled exception handler — log and show dialog rather than silent crash
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                string msg = args.ExceptionObject?.ToString() ?? "Unknown error";
                System.Diagnostics.Debug.WriteLine($"[App] Unhandled: {msg}");
                MessageBox.Show($"An unexpected error occurred:\n\n{msg}",
                    "GTA V Mod Manager — Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[App] Dispatcher: {args.Exception.Message}");
                MessageBox.Show($"An unexpected error occurred:\n\n{args.Exception.Message}",
                    "GTA V Mod Manager — Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
