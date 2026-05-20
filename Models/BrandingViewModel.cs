namespace Mvc.Models;

public sealed class BrandingViewModel
{
    public string CompanyPhone { get; set; } = string.Empty;

    public string SiteName { get; set; } = string.Empty;

    public string PaletteKey { get; set; } = BrandingDefaults.DefaultPaletteKey;

    public string PrimaryColor { get; set; } = BrandingDefaults.DefaultPrimaryColor;

    public string SecondaryColor { get; set; } = BrandingDefaults.DefaultSecondaryColor;

    public string BackgroundColor { get; set; } = BrandingDefaults.DefaultBackgroundColor;

    public string? LogoDataUrl { get; set; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class BrandingFormInput
{
    public string SiteName { get; set; } = string.Empty;

    public string PaletteKey { get; set; } = BrandingDefaults.DefaultPaletteKey;

    public bool RemoveLogo { get; set; }
}

public sealed record BrandingPaletteOption(
    string Key,
    string Label,
    string PrimaryColor,
    string SecondaryColor,
    string BackgroundColor);

public sealed record BrandingSettings(
    string StoreId,
    string SiteName,
    string PaletteKey,
    string PrimaryColor,
    string SecondaryColor,
    string BackgroundColor,
    string? LogoDataUrl,
    string UpdatedAtUtc);

public static class BrandingDefaults
{
    public const string DefaultPaletteKey = "TERRACOTA";
    public const string DefaultPrimaryColor = "#9F3D1E";
    public const string DefaultSecondaryColor = "#241914";
    public const string DefaultBackgroundColor = "#F6F3EF";

    public static IReadOnlyList<BrandingPaletteOption> PaletteOptions { get; } =
    [
        new("TERRACOTA", "Terracota", "#9F3D1E", "#241914", "#F6F3EF"),
        new("VERDE", "Verde", "#1F6F43", "#17231B", "#F3F7F2"),
        new("AZUL", "Azul", "#245B8F", "#172331", "#F2F6FA"),
        new("VINHO", "Vinho", "#7A1E34", "#26151B", "#FBF3F5"),
        new("GRAFITE", "Grafite", "#252A31", "#1F2328", "#F4F4F2")
    ];

    public static BrandingSettings CreateDefault(string storeId, string siteName)
    {
        return new BrandingSettings(
            string.IsNullOrWhiteSpace(storeId) ? string.Empty : storeId.Trim(),
            string.IsNullOrWhiteSpace(siteName) ? "Restaurantes" : siteName.Trim(),
            DefaultPaletteKey,
            DefaultPrimaryColor,
            DefaultSecondaryColor,
            DefaultBackgroundColor,
            LogoDataUrl: null,
            UpdatedAtUtc: string.Empty);
    }

    public static BrandingPaletteOption GetPalette(string? paletteKey)
    {
        var normalized = string.IsNullOrWhiteSpace(paletteKey)
            ? DefaultPaletteKey
            : paletteKey.Trim().ToUpperInvariant();

        return PaletteOptions.FirstOrDefault(palette => palette.Key == normalized) ??
            PaletteOptions[0];
    }
}
