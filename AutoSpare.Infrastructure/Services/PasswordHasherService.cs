using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace AutoSpare.Infrastructure.Services;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    // برای استفاده از متدهای PasswordHasher به یک نمونه کاربر فرمالیته نیاز داریم
    private static readonly AppUser DummyUser = null!;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("رمز عبور نمی‌تواند خالی باشد.", nameof(password));

        return _hasher.HashPassword(DummyUser, password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        var result = _hasher.VerifyHashedPassword(DummyUser, passwordHash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}

