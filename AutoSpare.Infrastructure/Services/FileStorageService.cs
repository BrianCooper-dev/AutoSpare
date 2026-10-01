using AutoSpare.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace AutoSpare.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private const long MaxFileSizeInBytes = 3 * 1024 * 1024; // حداکثر ۳ مگابایت
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public FileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveProductImageAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("فایل ارسال شده خالی است.", nameof(fileStream));

        if (fileStream.Length > MaxFileSizeInBytes)
            throw new InvalidOperationException("حجم تصویر نمی‌تواند بیشتر از ۳ مگابایت باشد.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("فرمت فایل نامعتبر است. تنها فرمت‌های JPG, PNG و WEBP مجاز هستند.");

        // مسیر وب‌روت
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", "products");

        // اطمینان از وجود دایرکتوری
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // نام‌گذاری امن و تصادفی برای جلوگیری از تداخل و مسایل امنیتی
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

        // ذخیره فایل روی دیسک
        await using (var destinationStream = new FileStream(physicalPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        // بازگرداندن مسیر وب
        return $"/uploads/products/{uniqueFileName}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        try
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cleanPath = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, cleanPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // عدم پرتاب خطا در زمان حذف اختیاری فایل
        }
    }
}

