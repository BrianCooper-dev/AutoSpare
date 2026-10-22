namespace AutoSpare.Application.Backup.DTOs;

public sealed record RestoreDatabaseResultDto(
    bool IsSuccess,
    string Message,
    string? TargetDatabase = null,
    string? RestoredFromPath = null);

