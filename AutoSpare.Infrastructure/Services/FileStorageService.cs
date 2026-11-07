using AutoSpare.Application.Common.Interfaces;
using AutoSpare.Application.Common.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AutoSpare.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;
    private readonly StorageSettings _storageSettings;

    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public FileStorageService(
        IWebHostEnvironment environment,
        ILogger<FileStorageService> logger,
        IOptions<StorageSettings> storageSettings)
    {
        _environment = environment;
        _logger = logger;
        _storageSettings = storageSettings.Value;
    }

    public async Task<string> SaveProductImageAsync(Stream fileStream, string originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (fileStream == null || !fileStream.CanRead)
        {
            throw new ArgumentException("فایل معتبر نیست.", nameof(fileStream));
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("فرمت فایل غیرمجاز است. فقط تصاویر (jpg, jpeg, png, webp) مجاز هستند.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        if (memoryStream.Length == 0)
        {
            throw new InvalidOperationException("فایل خالی است.");
        }

        if (memoryStream.Length > MaxFileSizeInBytes)
        {
            throw new InvalidOperationException(
                $"حجم فایل بیشتر از حد مجاز ({StorageSettings.SectionName} / ۵ مگابایت) است.");
        }

        if (!IsValidImageSignature(memoryStream))
        {
            throw new InvalidOperationException("محتوای فایل با فرمت تصویر مطابقت ندارد.");
        }

        memoryStream.Position = 0;

        var relativeImagesPath = string.IsNullOrWhiteSpace(_storageSettings.ImagesPath)
            ? "uploads/products"
            : _storageSettings.ImagesPath.TrimStart('/', '\\');

        var uploadsFolder =
            Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
                relativeImagesPath);

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using (var fileWriter = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                         useAsync: true))
        {
            await memoryStream.CopyToAsync(fileWriter, cancellationToken);
            await fileWriter.FlushAsync(cancellationToken);
        }

        var normalizedPath = Path.Combine(relativeImagesPath, uniqueFileName).Replace("\\", "/");
        return $"/{normalizedPath.TrimStart('/')}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cleanPath = relativePath.TrimStart('/', '~').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, cleanPath));

            var allowedRoot = Path.GetFullPath(webRoot);
            if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("تلاش غیرمجاز برای دسترسی به فایل خارج از ریشه وب: {Path}", relativePath);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در حذف فایل: {Path}", relativePath);
        }
    }

    private static bool IsValidImageSignature(MemoryStream stream)
    {
        byte[] header = new byte[12];
        int readCount = stream.Read(header, 0, header.Length);

        if (readCount < 4) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (readCount >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return true;
        }

        // WEBP: RIFF ???? WEBP
        if (readCount >= 12 &&
            header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
        {
            return true;
        }

        return false;
    }
}
