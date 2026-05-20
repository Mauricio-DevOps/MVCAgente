using System.Net.Http.Json;

namespace Mvc.Services;

public sealed class RestaurantMenuApiClient
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public RestaurantMenuApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SyncProductToMenuAsync(
        ProductMenuSyncRequest syncRequest,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/menu-items/sync-from-product")
        {
            Content = JsonContent.Create(syncRequest)
        };

        request.Headers.Add(ServiceKeyHeaderName, _configuration["InternalApi:ServiceKey"] ?? "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record ProductMenuSyncRequest(
    string StoreId,
    string ProductId,
    string Name,
    string? Description,
    decimal RetailPrice);
