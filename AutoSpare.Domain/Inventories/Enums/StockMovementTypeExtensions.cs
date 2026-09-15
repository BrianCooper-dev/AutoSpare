namespace AutoSpare.Domain.Inventories.Enums;

public static class StockMovementTypeExtensions
{
    /// <summary>
    /// جهت مجاز حرکت را برمی‌گرداند:
    /// true = فقط افزایشی، false = فقط کاهشی، null = هر دو جهت مجاز.
    /// </summary>
    public static bool? IncreasesStock(this StockMovementType type) => type switch
    {
        StockMovementType.Initial
            or StockMovementType.Purchase
            or StockMovementType.CustomerReturn
            or StockMovementType.TransferIn => true,

        StockMovementType.Sale
            or StockMovementType.SupplierReturn
            or StockMovementType.TransferOut => false,

        StockMovementType.Adjustment => null,

        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "نوع حرکت موجودی معتبر نیست.")
    };
}

