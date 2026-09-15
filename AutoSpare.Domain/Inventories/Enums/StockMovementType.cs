namespace AutoSpare.Domain.Inventories.Enums;

public enum StockMovementType
{
    /// <summary>موجودی اولیه هنگام تعریف کالا در انبار</summary>
    Initial = 1,

    /// <summary>خرید / ورود کالا به انبار</summary>
    Purchase = 2,

    /// <summary>فروش کالا</summary>
    Sale = 3,

    /// <summary>مرجوعی از مشتری (ورود به انبار)</summary>
    CustomerReturn = 4,

    /// <summary>مرجوعی به تامین‌کننده (خروج از انبار)</summary>
    SupplierReturn = 5,

    /// <summary>انتقال بین انبارها - ورود</summary>
    TransferIn = 6,

    /// <summary>انتقال بین انبارها - خروج</summary>
    TransferOut = 7,

    /// <summary>اصلاح دستی / انبارگردانی (هر دو جهت مجاز است)</summary>
    Adjustment = 8
}
