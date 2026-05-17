using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mvc.Options;

namespace Mvc.Services;

public sealed class SsoTokenService
{
    private readonly SsoOptions _options;

    public SsoTokenService(IOptions<SsoOptions> options)
    {
        _options = options.Value;
    }

    public bool TryValidate(string? token, out SsoPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            return false;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var expectedSignature = Sign(parts[0]);
        byte[] providedSignature;
        try
        {
            providedSignature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (providedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var parsed = JsonSerializer.Deserialize<SsoPayload>(json);
            if (parsed is null || !parsed.IsValid())
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(parsed.ExpiresUtc, out var expiresUtc) ||
                expiresUtc <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private byte[] Sign(string payloadPart)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart));
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed record SsoPayload(
    string RestaurantId,
    string RestaurantName,
    string CompanyPhone,
    string TargetPath,
    string ExpiresUtc,
    string? AccessMode)
{
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(RestaurantId) &&
            !string.IsNullOrWhiteSpace(RestaurantName) &&
            !string.IsNullOrWhiteSpace(CompanyPhone) &&
            !string.IsNullOrWhiteSpace(TargetPath) &&
            !string.IsNullOrWhiteSpace(ExpiresUtc);
    }
}
