using AutoSpare.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace AutoSpare.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private const long MaxFileSizeInBytes = 3 * 1024 * 1024; // ۳ مگابایت
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public FileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveProductImageAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        if (fileStream == null || !fileStream.CanRead)
            throw new ArgumentException("فایل ارسال شده خالی است.", nameof(fileStream));

        if (fileStream.Length == 0)
            throw new ArgumentException("فایل ارسال شده خالی است.", nameof(fileStream));

        if (fileStream.Length > MaxFileSizeInBytes)
            throw new InvalidOperationException("حجم تصویر نمی‌تواند بیشتر از ۳ مگابایت باشد.");

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("فرمت فایل نامعتبر است. تنها فرمت‌های JPG, PNG و WEBP مجاز هستند.");

        // اعتبارسنجی امضای باینری (MIME Sniffing)
        if (!IsValidImageSignature(fileStream, extension))
            throw new InvalidOperationException("فرمت فایل نامعتبر است. امضای باینری تصویر تطابق ندارد.");

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", "products");

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // نام‌گذاری تصادفی برای جلوگیری از تداخل و حملات تزریق مسیر
        var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        await using (var destinationStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return $"/uploads/products/{uniqueFileName}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        try
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var allowedFolder = Path.GetFullPath(Path.Combine(webRoot, "uploads", "products"));

            var cleanPath = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, cleanPath));

            // جلوگیری از Path Traversal: فایل باید حتماً درون پوشه مجاز باشد
            if (!fullPath.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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

    private static bool IsValidImageSignature(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[16];

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        int bytesRead = stream.Read(header);

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        var ext = extension.ToLowerInvariant();

        // JPEG: FF D8 FF
        if (ext is ".jpg" or ".jpeg")
        {
            return bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (ext is ".png")
        {
            return bytesRead >= 8 &&
                   header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                   header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        // WEBP: RIFF (بایت‌های ۰ تا ۳) و WEBP (بایت‌های ۸ تا ۱۱)
        if (ext is ".webp")
        {
            return bytesRead >= 12 &&
                   header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // RIFF
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // WEBP
        }

        return false;
    }
}
