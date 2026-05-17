namespace Mvc.Services;

public sealed class ExternalUrlResolver
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExternalUrlResolver(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public string BuildRestaurantUrl(string localPath)
    {
        var baseUrl = ResolveBaseUrl("ExternalLinks:RestaurantAdminBaseUrl", "http://localhost:5000");
        return $"{baseUrl}{NormalizeLocalPath(localPath)}";
    }

    public string GetRestaurantAdminBaseUrl()
    {
        return ResolveBaseUrl("ExternalLinks:RestaurantAdminBaseUrl", "http://localhost:5000");
    }

    private string ResolveBaseUrl(string configurationKey, string fallback)
    {
        var configured = (_configuration[configurationKey] ?? fallback).TrimEnd('/');
        if (!IsLocalConfiguredUrl(configured))
        {
            return configured;
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request?.Host.HasValue == true && !IsLocalHost(request.Host.Host))
        {
            return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
        }

        return configured;
    }

    private static string NormalizeLocalPath(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return "/";
        }

        return localPath.StartsWith("/", StringComparison.Ordinal) ? localPath : $"/{localPath}";
    }

    private static bool IsLocalConfiguredUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsLocalHost(uri.Host);
    }

    private static bool IsLocalHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
