using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mvc.Services;

public sealed class RestaurantAuthClient
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public RestaurantAuthClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<RestaurantWhatsAppLoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/whatsapp-auth/login")
        {
            Content = JsonContent.Create(new RestaurantWhatsAppLoginRequest(email, password))
        };
        request.Headers.Add(ServiceKeyHeaderName, _configuration["InternalApi:ServiceKey"] ?? "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        if (!IsJsonResponse(response))
        {
            throw new HttpRequestException(
                "The restaurant login service returned a non-JSON response. Check ExternalLinks:RestaurantAdminBaseUrl.");
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<RestaurantWhatsAppLoginResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("The restaurant login service returned invalid JSON.", ex);
        }
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType is not null &&
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record RestaurantWhatsAppLoginRequest(string Email, string Password);

public sealed record RestaurantWhatsAppLoginResponse(
    string CompanyId,
    string CompanyName,
    string CompanyPhone,
    string Username);
