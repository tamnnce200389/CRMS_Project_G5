
using CRMS.Data;
using CRMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace CRMS.Services
{
    public class AuthService
    {
        private readonly PasswordHasher<User> _hasher = new();

        // F02 - LOGIN
        public async Task<(bool Success, string Message)> LoginAsync(
            string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return (false, "Please enter email and password.");
            }

            using var db = CrmsContextFactory.Create();

            email = email.Trim().ToLowerInvariant();

            // Tim tai khoan trong SQL Server
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            const string invalidMessage =
                "Invalid email or password, or account is inactive.";

            if (user == null || !user.IsActive)
                return (false, invalidMessage);

            // Kiem tra mat khau da hash
            PasswordVerificationResult result;

            try
            {
                result = _hasher.VerifyHashedPassword(
                    user, user.PasswordHash, password);
            }
            catch (FormatException)
            {
                return (false, invalidMessage);
            }

            if (result == PasswordVerificationResult.Failed)
                return (false, invalidMessage);

            // Luu phien dang nhap
            CurrentUserService.Instance.SetUser(user);

            return (true, "Login successful!");
        }

        // F01 - REGISTER
        public async Task<(bool Success, string Message)> RegisterAsync(
            string fullName,
            string email,
            string? phone,
            string password,
            string confirmPassword)
        {
            fullName = fullName.Trim();
            email = email.Trim().ToLowerInvariant();
            phone = string.IsNullOrWhiteSpace(phone)
                ? null
                : phone.Trim();

            // Validate
            if (fullName.Length < 2 || fullName.Length > 120)
                return (false, "Full name must be 2-120 characters.");

            if (email.Length > 256 ||
                !MailAddress.TryCreate(email, out _))
                return (false, "Invalid email address.");

            if (phone?.Length > 20)
                return (false, "Phone number is too long.");

            if (password.Length < 10 || password.Length > 128)
                return (false, "Password must be 10-128 characters.");

            if (password != confirmPassword)
                return (false, "Passwords do not match.");

            using var db = CrmsContextFactory.Create();

            // Kiem tra email trung
            bool exists = await db.Users
                .AnyAsync(u => u.Email == email);

            if (exists)
                return (false, "Email already exists.");

            // Tao tai khoan Customer
            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phone,
                UserRole = "Customer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Ma hoa mat khau truoc khi luu
            user.PasswordHash = _hasher.HashPassword(
                user, password);

            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                return (false, "Email already exists.");
            }

            return (true, "Registration successful!");
        }
    }
}
