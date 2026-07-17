using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Data
{
    public static class DbContextFactory
    {
        public static AppDbContext Create()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=SQL,1433;Database=ClinicStatisticsDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=15;")
                .Options;

            return new AppDbContext(options);
        }
    }
}
