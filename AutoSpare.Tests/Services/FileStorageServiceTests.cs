using AutoSpare.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace AutoSpare.Tests.Services;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _testWebRoot;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly FileStorageService _fileStorageService;

    public FileStorageServiceTests()
    {
        _testWebRoot = Path.Combine(Path.GetTempPath(), "AutoSpareTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWebRoot);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.WebRootPath).Returns(_testWebRoot);

        _fileStorageService = new FileStorageService(_envMock.Object);
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
        // Arrange
        var fileContent = GetValidImageBytes(fileName);
        using var stream = new MemoryStream(fileContent);

        // Act
        var resultPath = await _fileStorageService.SaveProductImageAsync(stream, fileName);

        // Assert
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
        // Arrange
        var fileContent = new byte[] { 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream(fileContent);

        // Act
        var act = async () => await _fileStorageService.SaveProductImageAsync(stream, fileName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*فرمت فایل نامعتبر است*");
    }

    [Fact]
    public async Task SaveProductImage_WithFakePngSignature_ShouldThrowInvalidOperationException()
    {
        // Arrange: پسوند png ولی باینری غیراستاندارد
        var fakeContent = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };
        using var stream = new MemoryStream(fakeContent);

        // Act
        var act = async () => await _fileStorageService.SaveProductImageAsync(stream, "image.png");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*امضای باینری تصویر تطابق ندارد*");
    }

    [Fact]
    public async Task SaveProductImage_ExceedingMaxSize_ShouldThrowInvalidOperationException()
    {
        // Arrange: بیش از ۳ مگابایت
        var largeContent = new byte[3 * 1024 * 1024 + 10];
        using var stream = new MemoryStream(largeContent);

        // Act
        var act = async () => await _fileStorageService.SaveProductImageAsync(stream, "large_image.jpg");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*حجم تصویر نمی‌تواند بیشتر از ۳ مگابایت باشد*");
    }

    [Fact]
    public async Task SaveProductImage_WithEmptyStream_ShouldThrowArgumentException()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Act
        var act = async () => await _fileStorageService.SaveProductImageAsync(emptyStream, "empty.png");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*فایل ارسال شده خالی است*");
    }

    [Fact]
    public async Task DeleteFile_ExistingFile_ShouldRemoveFileFromDisk()
    {
        // Arrange: ایجاد فایل با امضای معتبر PNG
        var fileContent = GetValidImageBytes("photo.png");
        using var stream = new MemoryStream(fileContent);
        var relativePath = await _fileStorageService.SaveProductImageAsync(stream, "photo.png");

        var physicalPath = Path.Combine(_testWebRoot, relativePath.TrimStart('/'));
        File.Exists(physicalPath).Should().BeTrue();

        // Act
        _fileStorageService.DeleteFile(relativePath);

        // Assert
        File.Exists(physicalPath).Should().BeFalse();
    }

    [Fact]
    public void DeleteFile_WithPathTraversalAttack_ShouldNotDeleteOutsideUploads()
    {
        // Arrange: ایجاد یک فایل تستی بیرون از پوشه uploads
        var sensitiveFile = Path.Combine(_testWebRoot, "appsettings.json");
        File.WriteAllText(sensitiveFile, "{\"Secret\": \"value\"}");

        // Act: تلاش برای دسترسی به فایل بیرونی از طریق ../
        _fileStorageService.DeleteFile("/uploads/products/../../appsettings.json");

        // Assert: فایل بیرونی نباید حذف شود
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
