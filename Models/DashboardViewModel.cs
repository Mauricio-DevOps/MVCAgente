namespace Mvc.Models;

public sealed class DashboardViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DashboardResponse? Dashboard { get; set; }
}

public sealed record CompanyLoginResponse(
    string CompanyId,
    string CompanyName,
    string CompanyPhone,
    string Username);

public sealed record LoginRequest(string Username, string Password);

public sealed record DashboardResponse(
    int TotalOrders,
    long TotalSoldCents,
    long AverageTicketCents,
    DashboardTopProductResponse? TopProduct,
    int PendingReviewOrders,
    int LateOrders,
    IReadOnlyList<DashboardStatusCountResponse> StatusCounts,
    IReadOnlyList<DashboardTopProductResponse> TopProducts,
    IReadOnlyList<DashboardRecentOrderResponse> RecentOrders);

public sealed record DashboardTopProductResponse(
    string ProductName,
    int Quantity,
    long TotalCents);

public sealed record DashboardStatusCountResponse(string Status, int Count);

public sealed record DashboardRecentOrderResponse(
    string Id,
    string PhoneNumber,
    string Status,
    string? SaleType,
    long TotalCents,
    string? GeneralObservation,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    bool IsLate,
    IReadOnlyList<DashboardRecentOrderItemResponse> Items);

public sealed record DashboardRecentOrderItemResponse(
    string RequestedProductName,
    string? ProductName,
    int Quantity,
    long? UnitPriceCents,
    long? TotalPriceCents,
    string? Observation,
    string MatchStatus);
