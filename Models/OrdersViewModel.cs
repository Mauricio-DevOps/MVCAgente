namespace Mvc.Models;

public sealed class OrdersViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? SelectedStatus { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public IReadOnlyList<OrderManagementCustomerResponse> Customers { get; set; } =
        Array.Empty<OrderManagementCustomerResponse>();

    public IReadOnlyList<OrderHistoryImportErrorViewModel> ImportErrors { get; set; } =
        Array.Empty<OrderHistoryImportErrorViewModel>();

    public OrderHistoryImportSummaryViewModel? ImportSummary { get; set; }
}

public sealed record OrderManagementCustomerResponse(
    string PhoneNumber,
    int TotalOrders,
    int OpenOrders,
    string LastOrderAtUtc,
    IReadOnlyList<OrderManagementOrderResponse> Orders);

public sealed record OrderManagementOrderResponse(
    string Id,
    string Status,
    string? SaleType,
    long TotalCents,
    string? GeneralObservation,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    IReadOnlyList<OrderManagementOrderItemResponse> Items);

public sealed record OrderManagementOrderItemResponse(
    string RequestedProductName,
    string? ProductName,
    int Quantity,
    long? UnitPriceCents,
    long? TotalPriceCents,
    string? Observation,
    string MatchStatus);

public sealed record OrderStatusUpdateRequest(
    string StoreId,
    string Status);

public sealed record OrderHistoryImportRequest(
    string StoreId,
    IReadOnlyList<OrderHistoryImportRowRequest> Rows);

public sealed record OrderHistoryImportRowRequest(
    int RowNumber,
    string PedidoCodigo,
    string? PedidoData,
    string? ClienteNome,
    string? CpfCnpj,
    string? ClienteEmail,
    string ClienteTelefoneCelular,
    string? TipoVenda,
    string ProdutoNome,
    int Quantidade,
    decimal? PrecoUnitario,
    string? ObservacaoItem,
    string? ObservacaoPedido);

public sealed record OrderHistoryImportResponse(
    int CreatedOrderCount,
    int CreatedItemCount,
    int SkippedOrderCount,
    IReadOnlyList<OrderHistoryImportErrorViewModel> Errors);

public sealed record OrderHistoryImportErrorViewModel(int RowNumber, string Message);

public sealed record OrderHistoryImportSummaryViewModel(
    int CreatedOrderCount,
    int CreatedItemCount,
    int SkippedOrderCount);
