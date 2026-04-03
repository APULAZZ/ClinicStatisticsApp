using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class AuthService
    {
        public CurrentUserInfo? Authenticate(string login, string password)
        {
            using var db = DbContextFactory.Create();

            var user = db.Users
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.Login == login && u.IsActive);

            if (user == null)
                return null;

            // Временно: пароль хранится как текст
            if (user.PasswordHash != password)
                return null;

            return new CurrentUserInfo
            {
                UserId = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                RoleCode = user.Role?.Code ?? string.Empty,
                RoleName = user.Role?.Name ?? string.Empty,
                BranchId = user.BranchId,
                BranchName = user.Branch?.Name
            };
        }
    }
}