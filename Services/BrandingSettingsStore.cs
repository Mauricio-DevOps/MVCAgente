using System.Globalization;
using System.Text.Json;
using Mvc.Models;

namespace Mvc.Services;

public sealed class BrandingSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private readonly ILogger<BrandingSettingsStore> _logger;

    public BrandingSettingsStore(IWebHostEnvironment environment, ILogger<BrandingSettingsStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "Branding",
            "branding-settings.json");
    }

    public async Task<BrandingSettings?> GetAsync(string storeId, CancellationToken cancellationToken)
    {
        var normalizedStoreId = NormalizeText(storeId);
        if (normalizedStoreId is null)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var settings = await ReadAllUnsafeAsync(cancellationToken);
            return settings.TryGetValue(normalizedStoreId, out var value)
                ? NormalizeSettings(value)
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BrandingSettings> SaveAsync(
        string storeId,
        string siteName,
        string paletteKey,
        string? logoDataUrl,
        bool removeLogo,
        CancellationToken cancellationToken)
    {
        var normalizedStoreId = NormalizeText(storeId) ??
            throw new InvalidOperationException("Login sem telefone de empresa vinculado.");
        var normalizedSiteName = NormalizeText(siteName) ??
            throw new InvalidOperationException("Informe o nome do site.");

        if (normalizedSiteName.Length > 120)
        {
            throw new InvalidOperationException("O nome do site deve ter no maximo 120 caracteres.");
        }

        var palette = BrandingDefaults.GetPalette(paletteKey);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var settings = await ReadAllUnsafeAsync(cancellationToken);
            settings.TryGetValue(normalizedStoreId, out var existing);

            var saved = new BrandingSettings(
                normalizedStoreId,
                normalizedSiteName,
                palette.Key,
                palette.PrimaryColor,
                palette.SecondaryColor,
                palette.BackgroundColor,
                removeLogo ? null : logoDataUrl ?? existing?.LogoDataUrl,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            settings[normalizedStoreId] = saved;
            await WriteAllUnsafeAsync(settings, cancellationToken);
            return saved;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, BrandingSettings>> ReadAllUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, BrandingSettings>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<Dictionary<string, BrandingSettings>>(
                stream,
                JsonOptions,
                cancellationToken);

            return settings is null
                ? new Dictionary<string, BrandingSettings>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, BrandingSettings>(settings, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(error, "Nao foi possivel ler as configuracoes locais de marca.");
            return new Dictionary<string, BrandingSettings>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteAllUnsafeAsync(
        Dictionary<string, BrandingSettings> settings,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_filePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static BrandingSettings NormalizeSettings(BrandingSettings settings)
    {
        var palette = BrandingDefaults.GetPalette(settings.PaletteKey);
        return settings with
        {
            SiteName = string.IsNullOrWhiteSpace(settings.SiteName) ? "Restaurantes" : settings.SiteName.Trim(),
            PaletteKey = palette.Key,
            PrimaryColor = palette.PrimaryColor,
            SecondaryColor = palette.SecondaryColor,
            BackgroundColor = palette.BackgroundColor
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
