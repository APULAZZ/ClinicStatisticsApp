using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class EmployeeService
    {
        public List<Employee> GetAll()
        {
            using var db = DbContextFactory.Create();

            return db.Employees
                .AsNoTracking()
                .OrderBy(e => e.FullName)
                .ToList();
        }

        public Employee? GetById(int id)
        {
            using var db = DbContextFactory.Create();

            return db.Employees
                .FirstOrDefault(e => e.Id == id);
        }

        public void Add(Employee employee)
        {
            using var db = DbContextFactory.Create();

            db.Employees.Add(employee);
            db.SaveChanges();
        }

        public void Update(Employee employee)
        {
            using var db = DbContextFactory.Create();

            var existing = db.Employees.FirstOrDefault(e => e.Id == employee.Id);
            if (existing == null)
                return;

            existing.FullName = employee.FullName;
            existing.IsActive = employee.IsActive;
            existing.IsCallCenter = employee.IsCallCenter;
            existing.DefaultReviewPaymentRate = employee.DefaultReviewPaymentRate;
            existing.Comment = employee.Comment;
            existing.UpdatedAt = System.DateTime.Now;

            db.SaveChanges();
        }
    }
}