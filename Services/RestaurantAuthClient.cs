using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mvc.Services;

public sealed class RestaurantAuthClient
{
    private const string ServiceKeyHeaderName = "X-Internal-Service-Key";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RestaurantAuthClient> _logger;

    public RestaurantAuthClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RestaurantAuthClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RestaurantWhatsAppLoginResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        const string path = "/api/internal/whatsapp-auth/login";
        _logger.LogInformation("Restaurant login request starting. BaseAddress={BaseAddress}; Path={Path}.", _httpClient.BaseAddress, path);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new RestaurantWhatsAppLoginRequest(email, password))
        };
        request.Headers.Add(ServiceKeyHeaderName, _configuration["InternalApi:ServiceKey"] ?? "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        _logger.LogInformation(
            "Restaurant login response received. RequestUri={RequestUri}; StatusCode={StatusCode}; ReasonPhrase={ReasonPhrase}; ContentType={ContentType}.",
            response.RequestMessage?.RequestUri,
            (int)response.StatusCode,
            response.ReasonPhrase,
            response.Content.Headers.ContentType?.MediaType);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Conflict)
        {
            _logger.LogInformation("Restaurant login returned an expected authentication status. StatusCode={StatusCode}.", (int)response.StatusCode);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Restaurant login returned non-success status. StatusCode={StatusCode}; ReasonPhrase={ReasonPhrase}.",
                (int)response.StatusCode,
                response.ReasonPhrase);
        }

        response.EnsureSuccessStatusCode();
        if (!IsJsonResponse(response))
        {
            _logger.LogError("Restaurant login returned non-JSON content. ContentType={ContentType}.", response.Content.Headers.ContentType?.MediaType);
            throw new HttpRequestException(
                "The restaurant login service returned a non-JSON response. Check ExternalLinks:RestaurantAdminBaseUrl.");
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<RestaurantWhatsAppLoginResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Restaurant login returned invalid JSON.");
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
