using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Data
{
    public static class DbContextFactory
    {
        public static AppDbContext Create()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=SQL;Database=ClinicStatisticsDb;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            return new AppDbContext(options);
        }
    }
}