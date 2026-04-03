using System;
using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Role? Role { get; set; }
        public Branch? Branch { get; set; }

        public ICollection<BranchReport> CreatedBranchReports { get; set; } = new List<BranchReport>();
    }
}