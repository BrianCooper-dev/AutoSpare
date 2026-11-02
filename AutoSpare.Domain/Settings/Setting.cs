using AutoSpare.Domain.Common;

namespace AutoSpare.Domain.Settings;

public class Setting : BaseEntity
{
    public string StoreName { get; private set; } = "فروشگاه لوازم یدکی خودرو";
    public string Phone { get; private set; } = "۰۲۱-۱۲۳۴۵۶۷۸";
    public string Mobile { get; private set; } = "۰۹۱۲۰۰۰۰۰۰۰";
    public string Address { get; private set; } = "تهران، خیابان چراغ برق، پلاک ۱";

    public string FooterNote { get; private set; } =
        "اجناس فروخته شده در صورت باز نشدن پلمپ تا ۴۸ ساعت قابل مرجوع می‌باشد.";

    private Setting() { } // سازنده برای EF Core

    public Setting(string storeName, string phone, string mobile, string address, string footerNote)
    {
        StoreName = storeName;
        Phone = phone;
        Mobile = mobile;
        Address = address;
        FooterNote = footerNote;
    }

    public void Update(string storeName, string phone, string mobile, string address, string footerNote)
    {
        StoreName = storeName;
        Phone = phone;
        Mobile = mobile;
        Address = address;
        FooterNote = footerNote;
        UpdateModificationTime();
    }
}
