using System.Configuration;
using System.Data;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static BusyService Busy { get; } = new();
    }

}
