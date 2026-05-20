using System.Security.Claims;

namespace Mvc;

public static class PortalAccess
{
    private static readonly HashSet<string> AllowedWhatsAppTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Dashboard",
        "/Products",
        "/Customers",
        "/Conversations",
        "/Orders",
        "/Agent"
    };

    public static string NormalizeAccessMode(string? value)
    {
        return value?.Trim() switch
        {
            "SoRestaurante" => "SoRestaurante",
            "SoWhatsApp" => "SoWhatsApp",
            _ => "Ambos"
        };
    }

    public static bool HasRestaurantAccess(ClaimsPrincipal user)
    {
        return HasRestaurantAccess(NormalizeAccessMode(user.FindFirst(AuthClaims.RestaurantAccessMode)?.Value));
    }

    public static bool HasRestaurantAccess(string accessMode)
    {
        return !string.Equals(accessMode, "SoWhatsApp", StringComparison.Ordinal);
    }

    public static bool HasWhatsAppAccess(ClaimsPrincipal user)
    {
        return HasWhatsAppAccess(NormalizeAccessMode(user.FindFirst(AuthClaims.RestaurantAccessMode)?.Value));
    }

    public static bool HasWhatsAppAccess(string accessMode)
    {
        return !string.Equals(accessMode, "SoRestaurante", StringComparison.Ordinal);
    }

    public static string NormalizeWhatsAppTargetOrDefault(string? target)
    {
        return TryNormalizeWhatsAppTarget(target, out var normalizedTarget)
            ? normalizedTarget
            : "/Dashboard";
    }

    public static bool TryNormalizeWhatsAppTarget(string? target, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var trimmed = RemoveFragment(target.Trim());
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var queryIndex = trimmed.IndexOf('?');
        var path = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
        var query = queryIndex >= 0 ? trimmed[(queryIndex + 1)..] : string.Empty;
        if (!AllowedWhatsAppTargets.Contains(path))
        {
            return false;
        }

        normalizedTarget = string.IsNullOrEmpty(query)
            ? path
            : $"{path}?{query}";
        return true;
    }

    private static string RemoveFragment(string value)
    {
        var fragmentIndex = value.IndexOf('#');
        return fragmentIndex >= 0 ? value[..fragmentIndex] : value;
    }
}
