using AutoSpare.Application.Settings.DTOs;

namespace AutoSpare.Application.Settings;

public interface ISettingService
{
    Task<SettingDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(SettingDto dto, CancellationToken cancellationToken = default);
}
