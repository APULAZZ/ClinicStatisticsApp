using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryBookWindow : Window
    {
        public SummaryBookWindow()
        {
            InitializeComponent();
        }

        private void SummaryGeneralButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryGeneralWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryProfoButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryProfoWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryAdminButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryAdminWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryProDoctorButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryProDoctorWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void DynamicsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new DynamicsWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ComparativePerkButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ComparativePerkWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void AbsolutePrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AbsolutePrimaryWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ComparativeProfiButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ComparativeProfiWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void StubButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Этот сводный лист будет подключен следующим этапом.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}