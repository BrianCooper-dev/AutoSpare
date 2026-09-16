using AutoSpare.Domain.Common;

namespace AutoSpare.Domain.Users;

/// <summary>
/// کاربر ادمین / مدیر سیستم (سیستم تک‌کاربره مغازه)
/// </summary>
public class AppUser : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? QuickPinHash { get; private set; } // پین کد سریع برای باز کردن قفل صفحه در مغازه (اختیاری)
    public DateTime? LastLoginDate { get; private set; }

    private AppUser() { }

    public AppUser(string fullName, string username, string passwordHash, string? quickPinHash = null)
    {
        UpdateProfile(fullName);
        SetUsername(username);
        SetPasswordHash(passwordHash);
        QuickPinHash = quickPinHash;
    }

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("نام مدیر نمی‌تواند خالی باشد.", nameof(fullName));

        FullName = fullName.Trim();
    }

    public void SetUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            throw new ArgumentException("نام کاربری باید حداقل ۳ کاراکتر باشد.", nameof(username));

        Username = username.Trim().ToLowerInvariant();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("هش کلمه عبور نمی‌تواند خالی باشد.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }

    public void SetQuickPinHash(string? pinHash)
    {
        QuickPinHash = pinHash;
    }

    public void RecordLogin()
    {
        LastLoginDate = DateTime.UtcNow;
    }
}
