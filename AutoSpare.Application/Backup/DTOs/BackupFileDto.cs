namespace AutoSpare.Application.Backup.DTOs;


public sealed record BackupFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeInBytes { get; init; }
    public string FileSizeFormatted { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string PersianCreatedAt { get; init; } = string.Empty;
}
