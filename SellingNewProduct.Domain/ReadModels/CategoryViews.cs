namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for a category list screen, enriched with how many products the
/// category contains and the total value of their stock (SUM of price x stock quantity).
/// A LEFT JOIN + GROUP BY over Categories x Products. Read side only.
/// </summary>
public sealed record CategorySummaryView(
    Guid CategoryId,
    string Name,
    string Description,
    int ProductCount,
    decimal TotalStockValue,
    string Status);
