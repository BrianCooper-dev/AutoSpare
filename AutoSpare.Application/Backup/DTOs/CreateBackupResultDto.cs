namespace AutoSpare.Application.Backup.DTOs;

public sealed record CreateBackupResultDto(
    bool IsSuccess,
    string Message,
    string? FileName = null,
    string? FullPath = null,
    long? SizeInBytes = null);
