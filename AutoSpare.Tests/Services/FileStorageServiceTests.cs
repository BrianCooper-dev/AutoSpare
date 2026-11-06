using AutoSpare.Application.Common.Settings;
using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AutoSpare.Tests.Services;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _testWebRoot;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly Mock<ILogger<FileStorageService>> _loggerMock;
    private readonly IOptions<StorageSettings> _storageSettingsOptions;
    private readonly FileStorageService _fileStorageService;

    public FileStorageServiceTests()
    {
        _testWebRoot = Path.Combine(Path.GetTempPath(), "AutoSpareTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWebRoot);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.WebRootPath).Returns(_testWebRoot);

        _loggerMock = new Mock<ILogger<FileStorageService>>();

        _storageSettingsOptions = Options.Create(new StorageSettings
        {
            ImagesPath = "uploads/products"
        });

        _fileStorageService = new FileStorageService(
            _envMock.Object,
            _loggerMock.Object,
            _storageSettingsOptions);
    }

    private static byte[] GetValidImageBytes(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 },
            ".png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            ".webp" => new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 },
            _ => throw new ArgumentException("فرمت تستی پشتیبانی‌نشده", nameof(fileName))
        };
    }

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.jpeg")]
    [InlineData("test.png")]
    [InlineData("test.webp")]
    public async Task SaveProductImage_WithValidExtensionsAndSignatures_ShouldSaveFileSuccessfully(string fileName)
    {
        var fileContent = GetValidImageBytes(fileName);
        using var stream = new MemoryStream(fileContent);

        var resultPath = await _fileStorageService.SaveProductImageAsync(stream, fileName);

        resultPath.Should().StartWith("/uploads/products/");
        var physicalPath = Path.Combine(_testWebRoot, resultPath.TrimStart('/'));
        File.Exists(physicalPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("malicious.exe")]
    [InlineData("script.sh")]
    [InlineData("document.pdf")]
    [InlineData("archive.zip")]
    public async Task SaveProductImage_WithInvalidExtension_ShouldThrowInvalidOperationException(string fileName)
    {
        var fileContent = new byte[] { 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream(fileContent);

        Func<Task> act = async () => await _fileStorageService.SaveProductImageAsync(stream, fileName);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*فرمت فایل غیرمجاز است*");
    }

    [Fact]
    public async Task SaveProductImage_WithFakePngSignature_ShouldThrowInvalidOperationException()
    {
        var fakeContent = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };
        using var stream = new MemoryStream(fakeContent);

        Func<Task> act = async () => await _fileStorageService.SaveProductImageAsync(stream, "image.png");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*محتوای فایل با فرمت تصویر مطابقت ندارد*");
    }

    [Fact]
    public async Task SaveProductImage_ExceedingMaxSize_ShouldThrowInvalidOperationException()
    {
        var largeContent = new byte[6 * 1024 * 1024];
        using var stream = new MemoryStream(largeContent);

        Func<Task> act = async () => await _fileStorageService.SaveProductImageAsync(stream, "large_image.jpg");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*حجم فایل بیشتر از حد مجاز*");
    }

    [Fact]
    public async Task SaveProductImage_WithEmptyStream_ShouldThrowArgumentExceptionOrInvalidOp()
    {
        using var emptyStream = new MemoryStream();

        Func<Task> act = async () => await _fileStorageService.SaveProductImageAsync(emptyStream, "empty.png");

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DeleteFile_ExistingFile_ShouldRemoveFileFromDisk()
    {
        var fileContent = GetValidImageBytes("photo.png");
        using var stream = new MemoryStream(fileContent);
        var relativePath = await _fileStorageService.SaveProductImageAsync(stream, "photo.png");

        var physicalPath = Path.Combine(_testWebRoot, relativePath.TrimStart('/'));
        File.Exists(physicalPath).Should().BeTrue();

        _fileStorageService.DeleteFile(relativePath);

        File.Exists(physicalPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFile_WithPathTraversalAttack_ShouldNotDeleteOutsideUploads()
    {
        // فایل حساس را درست در ریشه‌ی وب‌روت (خارج از پوشه آپلود uploads/products) قرار می‌دهیم
        // مسیر حمله: /uploads/products/../appsettings.json (یک سطح بالا، یعنی خارج از پوشه آپلود اما درون وب‌روت)
        var sensitiveFile = Path.Combine(_testWebRoot, "appsettings.json");
        File.WriteAllText(sensitiveFile, "{\"Secret\": \"value\"}");

        // تلاش برای پاکسازی با مسیر مخرب (یک سطح بالاتر از پوشه آپلود)
        _fileStorageService.DeleteFile("/uploads/products/../appsettings.json");

        // سرویس باید از حذف فایل‌های خارج از محدوده آپلود جلوگیری کند
        File.Exists(sensitiveFile).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWebRoot))
        {
            Directory.Delete(_testWebRoot, true);
        }
    }
}
