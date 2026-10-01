namespace AutoSpare.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// ذخیره ایمن تصویر، کنترل پسوند و حجم، و بازگرداندن مسیر نسبی
    /// </summary>
    Task<string> SaveProductImageAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// حذف فایل در صورت انصراف یا خطا
    /// </summary>
    void DeleteFile(string? relativePath);
}
