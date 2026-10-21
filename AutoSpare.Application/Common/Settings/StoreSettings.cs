namespace AutoSpare.Application.Common.Settings;

public sealed class StoreSettings
{
    public const string SectionName = "StoreSettings";

    public string StoreName { get; set; } = "فروشگاه لوازم یدکی خودرو";
    public string Phone { get; set; } = "۰۲۱-۱۲۳۴۵۶۷۸";
    public string Mobile { get; set; } = "۰۹۱۲۰۰۰۰۰۰۰";
    public string Address { get; set; } = "تهران، خیابان چراغ برق، پلاک ۱";
    public string FooterNote { get; set; } = "اجناس فروخته شده در صورت باز نشدن پلمپ تا ۴۸ ساعت قابل مرجوع می‌باشد.";
}
