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

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.jpeg")]
    [InlineData("test.png")]
    [InlineData("test.webp")]
    public async Task SaveProductImage_WithValidExtensions_ShouldSaveFileSuccessfully(string fileName)
    {
        // Arrange
        var fileContent = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01 }; // نمونه بایت‌های تصویر
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
    public async Task SaveProductImage_ExceedingMaxSize_ShouldThrowInvalidOperationException()
    {
        // Arrange: بیش از ۳ مگابایت (3 * 1024 * 1024 + 10)
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
        // Arrange
        var fileContent = new byte[] { 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream(fileContent);
        var relativePath = await _fileStorageService.SaveProductImageAsync(stream, "photo.png");

        var physicalPath = Path.Combine(_testWebRoot, relativePath.TrimStart('/'));
        File.Exists(physicalPath).Should().BeTrue();

        // Act
        _fileStorageService.DeleteFile(relativePath);

        // Assert
        File.Exists(physicalPath).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWebRoot))
        {
            Directory.Delete(_testWebRoot, true);
        }
    }
}
