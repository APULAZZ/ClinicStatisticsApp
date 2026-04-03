namespace ClinicStatisticsApp.Models
{
    public class SelectedBranchContext
    {
        public CurrentUserInfo CurrentUser { get; set; } = null!;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }
}