using AutoSpare.Application.Settings;
using AutoSpare.Application.Settings.DTOs;
using AutoSpare.Domain.Settings;
using AutoSpare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoSpare.Infrastructure.Services;

public class SettingService : ISettingService
{
    private readonly ApplicationDbContext _context;

    public SettingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SettingDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.Settings.FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            setting = new Setting(
                storeName: "فروشگاه لوازم یدکی خودرو",
                phone: "۰۲۱-۱۲۳۴۵۶۷۸",
                mobile: "۰۹۱۲۰۰۰۰۰۰۰",
                address: "تهران، خیابان چراغ برق، پلاک ۱",
                footerNote: "اجناس فروخته شده در صورت باز نشدن پلمپ تا ۴۸ ساعت قابل مرجوع می‌باشد."
            );

            _context.Settings.Add(setting);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SettingDto
        {
            StoreName = setting.StoreName,
            Phone = setting.Phone,
            Mobile = setting.Mobile,
            Address = setting.Address,
            FooterNote = setting.FooterNote
        };
    }

    public async Task UpdateSettingsAsync(SettingDto dto, CancellationToken cancellationToken = default)
    {
        var setting = await _context.Settings.FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            setting = new Setting(
                dto.StoreName,
                dto.Phone,
                dto.Mobile,
                dto.Address,
                dto.FooterNote
            );
            _context.Settings.Add(setting);
        }
        else
        {
            setting.Update(
                dto.StoreName,
                dto.Phone,
                dto.Mobile,
                dto.Address,
                dto.FooterNote
            );
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
