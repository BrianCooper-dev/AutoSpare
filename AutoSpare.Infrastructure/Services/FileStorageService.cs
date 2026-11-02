using AutoSpare.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;
    private const long MaxFileSizeInBytes = 3 * 1024 * 1024; // ۳ مگابایت

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveProductImageAsync(Stream fileStream, string originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null || !fileStream.CanRead)
            throw new ArgumentException("فایل ارسال شده خالی است.", nameof(fileStream));

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("فرمت فایل نامعتبر است. تنها فرمت‌های JPG, PNG و WEBP مجاز هستند.");

        // جهت سازگاری با Blazor InputFile که استریم آن Non-seekable است
        Stream workingStream = fileStream;
        MemoryStream? memoryStream = null;

        try
        {
            if (!fileStream.CanSeek)
            {
                memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                workingStream = memoryStream;
            }

            if (workingStream.Length == 0)
                throw new ArgumentException("فایل ارسال شده خالی است.", nameof(fileStream));

            if (workingStream.Length > MaxFileSizeInBytes)
                throw new InvalidOperationException("حجم تصویر نمی‌تواند بیشتر از ۳ مگابایت باشد.");

            // بررسی امضای باینری
            if (!IsValidImageSignature(workingStream, extension))
                throw new InvalidOperationException(
                    "فرمت فایل نامعتبر است. محتوای باینری فایل با پسوند آن همخوانی ندارد.");

            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRoot, "uploads", "products");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            workingStream.Seek(0, SeekOrigin.Begin);

            await using (var destinationStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write,
                             FileShare.None, 4096, useAsync: true))
            {
                await workingStream.CopyToAsync(destinationStream, cancellationToken);
            }

            return $"/uploads/products/{uniqueFileName}";
        }
        finally
        {
            if (memoryStream != null)
            {
                await memoryStream.DisposeAsync();
            }
        }
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

            // جلوگیری از حمله Directory Traversal
            if (!fullPath.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("درخواست نامعتبر برای حذف فایل خارج از مسیر مجاز: {Path}", relativePath);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در حذف فایل تصویر: {Path}", relativePath);
        }
    }

    private static bool IsValidImageSignature(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[16];

        stream.Seek(0, SeekOrigin.Begin);
        int bytesRead = stream.Read(header);
        stream.Seek(0, SeekOrigin.Begin);

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

        // WEBP: RIFF....WEBP
        if (ext is ".webp")
        {
            return bytesRead >= 12 &&
                   header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // RIFF
                   header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // WEBP
        }

        return false;
    }
}
