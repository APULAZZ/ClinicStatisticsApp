using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClinicStatisticsApp.Models
{
    public class ReviewEntryViewModel : INotifyPropertyChanged
    {
        private int? _id;
        private int _employeeId;
        private string _employeeFullName = string.Empty;
        private int _smsSentCount;
        private int _reviewsLeftCount;

        public int? Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public int EmployeeId
        {
            get => _employeeId;
            set => SetField(ref _employeeId, value);
        }

        public string EmployeeFullName
        {
            get => _employeeFullName;
            set => SetField(ref _employeeFullName, value);
        }

        public int SmsSentCount
        {
            get => _smsSentCount;
            set => SetField(ref _smsSentCount, value);
        }

        public int ReviewsLeftCount
        {
            get => _reviewsLeftCount;
            set => SetField(ref _reviewsLeftCount, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}