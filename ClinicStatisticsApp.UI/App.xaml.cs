using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using ClinicStatisticsApp.Services;

namespace ClinicStatisticsApp.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static BusyService Busy { get; } = new();
        private readonly FirebirdScheduledImportService _firebirdSchedule = new();

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Startup += (_, _) => _firebirdSchedule.Start();
            Exit += (_, _) => _firebirdSchedule.Dispose();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is OperationCanceledException)
            {
                e.Handled = true;
            }
        }
    }

}
