using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClinicStatisticsApp.Models
{
    public class NaradEntryViewModel : INotifyPropertyChanged
    {
        private int? _id;
        private int _employeeId;
        private string _employeeFullName = string.Empty;
        private int _smsSentCount;
        private int _reviewsLeftCount;
        private decimal _paymentPerReview;
        private bool _isIncluded;

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
            set
            {
                if (SetField(ref _smsSentCount, value))
                {
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        public int ReviewsLeftCount
        {
            get => _reviewsLeftCount;
            set
            {
                if (SetField(ref _reviewsLeftCount, value))
                {
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        public decimal PaymentPerReview
        {
            get => _paymentPerReview;
            set
            {
                if (SetField(ref _paymentPerReview, value))
                {
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                if (SetField(ref _isIncluded, value))
                {
                    OnPropertyChanged(nameof(TotalPayment));
                }
            }
        }

        public decimal TotalPayment => IsIncluded ? ReviewsLeftCount * PaymentPerReview : 0m;

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