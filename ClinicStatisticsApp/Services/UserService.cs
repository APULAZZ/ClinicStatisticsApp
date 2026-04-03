using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class UserService
    {
        public List<User> GetAll()
        {
            using var db = DbContextFactory.Create();

            return db.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .OrderBy(u => u.Login)
                .ToList();
        }

        public User? GetById(int id)
        {
            using var db = DbContextFactory.Create();

            return db.Users
                .FirstOrDefault(u => u.Id == id);
        }

        public List<Role> GetRoles()
        {
            using var db = DbContextFactory.Create();

            return db.Roles
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .ToList();
        }

        public List<Branch> GetBranches()
        {
            using var db = DbContextFactory.Create();

            return db.Branches
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .ToList();
        }

        public void Add(User user)
        {
            using var db = DbContextFactory.Create();

            if (db.Users.Any(u => u.Login == user.Login))
                throw new Exception("Пользователь с таким логином уже существует.");

            db.Users.Add(user);
            db.SaveChanges();
        }

        public void Update(User user)
        {
            using var db = DbContextFactory.Create();

            var existing = db.Users.FirstOrDefault(u => u.Id == user.Id);
            if (existing == null)
                throw new Exception("Пользователь не найден.");

            if (db.Users.Any(u => u.Login == user.Login && u.Id != user.Id))
                throw new Exception("Пользователь с таким логином уже существует.");

            existing.Login = user.Login;
            existing.PasswordHash = user.PasswordHash;
            existing.FullName = user.FullName;
            existing.RoleId = user.RoleId;
            existing.BranchId = user.BranchId;
            existing.IsActive = user.IsActive;
            existing.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }

        public void Delete(int userId)
        {
            using var db = DbContextFactory.Create();

            var user = db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                throw new Exception("Пользователь не найден.");

            db.Users.Remove(user);
            db.SaveChanges();
        }

        public void SetActive(int userId, bool isActive)
        {
            using var db = DbContextFactory.Create();

            var user = db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                throw new Exception("Пользователь не найден.");

            user.IsActive = isActive;
            user.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }
    }
}