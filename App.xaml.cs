using System;
using System.Windows;
using GtavModManager.Cli;

namespace GtavModManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // If any arguments are passed, run as CLI and exit — no GUI
            if (e.Args.Length > 0)
            {
                int exitCode = CliHandler.Run(e.Args);
                Shutdown(exitCode);
                return;
            }

            // GUI mode — wire up error handlers and show main window
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

            new MainWindow().Show();
        }
    }
}
