using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClinicStatisticsApp.Models
{
    public class PerkEntryViewModel : INotifyPropertyChanged
    {
        private int? _id;
        private int _employeeId;
        private string _employeeFullName = string.Empty;
        private int _attendanceCount;
        private int _absenceCount;

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

        public int AttendanceCount
        {
            get => _attendanceCount;
            set
            {
                if (SetField(ref _attendanceCount, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public int AbsenceCount
        {
            get => _absenceCount;
            set
            {
                if (SetField(ref _absenceCount, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public int Total => AttendanceCount + AbsenceCount;

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