using System.ComponentModel.DataAnnotations;

namespace Mvc.Models;

public sealed class ProductsViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public ProductFormModel Form { get; set; } = new();

    public IReadOnlyList<ProductResponse> Products { get; set; } = Array.Empty<ProductResponse>();

    public ProductImportPreviewModel? ImportPreview { get; set; }

    public IReadOnlyList<ProductImportErrorViewModel> ImportErrors { get; set; } =
        Array.Empty<ProductImportErrorViewModel>();

    public ProductImportSummaryViewModel? ImportSummary { get; set; }
}

public sealed class ProductFormModel
{
    public string? ProductId { get; set; }

    [Required(ErrorMessage = "Informe o nome do produto.")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "O preço de varejo não pode ser negativo.")]
    public decimal RetailPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O preco promocional nao pode ser negativo.")]
    public decimal? PromotionalPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O preço de atacado não pode ser negativo.")]
    public decimal WholesalePrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O estoque nao pode ser negativo.")]
    public int? StockQuantity { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O limite de estoque baixo nao pode ser negativo.")]
    public int? LowStockThreshold { get; set; }

    public string AliasesText { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool SyncToMenu { get; set; }

    public bool IsEditing => !string.IsNullOrWhiteSpace(ProductId);
}

public sealed record ProductUpsertRequest(
    string StoreId,
    string Name,
    string? Description,
    string? Type,
    string? Brand,
    decimal RetailPrice,
    decimal? PromotionalPrice,
    decimal WholesalePrice,
    IReadOnlyList<string>? Aliases,
    int? StockQuantity,
    int? LowStockThreshold,
    bool IsActive = true);

public static class ProductImportActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Skip = "Skip";
}

public sealed class ProductImportPreviewModel
{
    public List<ProductImportRowInput> Rows { get; set; } = [];

    public bool HasDuplicates => Rows.Any(row => row.IsDuplicate);

    public int ValidRowsCount => Rows.Count;

    public int DuplicateRowsCount => Rows.Count(row => row.IsDuplicate);
}

public sealed class ProductImportRowInput
{
    public int RowNumber { get; set; }

    public string Action { get; set; } = ProductImportActions.Create;

    public string? ExistingProductId { get; set; }

    public string? ExistingProductName { get; set; }

    public string? NewName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public decimal RetailPrice { get; set; }

    public decimal? PromotionalPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDuplicate => !string.IsNullOrWhiteSpace(ExistingProductId);
}

public sealed record ProductImportRequest(
    string StoreId,
    IReadOnlyList<ProductImportRowRequest> Rows);

public sealed record ProductImportRowRequest(
    int RowNumber,
    string Action,
    string? ProductId,
    string Name,
    string? Description,
    string? Type,
    string? Brand,
    decimal RetailPrice,
    decimal? PromotionalPrice,
    bool IsActive);

public sealed record ProductImportResponse(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<ProductImportErrorViewModel> Errors);

public sealed record ProductImportErrorViewModel(int RowNumber, string Message);

public sealed record ProductImportSummaryViewModel(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount);
