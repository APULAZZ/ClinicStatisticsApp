namespace ClinicStatisticsApp.Models
{
    public class CurrentUserInfo
    {
        public int UserId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }
}