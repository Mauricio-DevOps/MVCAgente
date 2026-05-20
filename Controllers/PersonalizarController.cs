using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

[Authorize]
[RequireWhatsAppPortalAccess]
public sealed class PersonalizarController : Controller
{
    private const long MaxLogoBytes = 512 * 1024;
    private readonly BrandingSettingsStore _brandingSettingsStore;

    public PersonalizarController(BrandingSettingsStore brandingSettingsStore)
    {
        _brandingSettingsStore = brandingSettingsStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await CreateModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        BrandingFormInput input,
        IFormFile? logo,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return View(model);
        }

        string? logoDataUrl = null;
        try
        {
            logoDataUrl = await ReadLogoDataUrlAsync(logo, cancellationToken);

            var saved = await _brandingSettingsStore.SaveAsync(
                model.CompanyPhone,
                input.SiteName,
                input.PaletteKey,
                logoDataUrl,
                input.RemoveLogo && logoDataUrl is null,
                cancellationToken);

            model = CreateModelFromBranding(saved);
            model.SuccessMessage = "Personalizacao salva.";
        }
        catch (InvalidOperationException error)
        {
            ApplyInput(model, input);
            model.ErrorMessage = error.Message;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ApplyInput(model, input);
            model.ErrorMessage = "Nao foi possivel salvar a personalizacao local.";
        }

        return View(model);
    }

    private async Task<BrandingViewModel> CreateModelAsync(CancellationToken cancellationToken)
    {
        var companyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty;
        var companyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Restaurantes";
        if (string.IsNullOrWhiteSpace(companyPhone))
        {
            return new BrandingViewModel
            {
                SiteName = companyName,
                ErrorMessage = "Login sem telefone de empresa vinculado."
            };
        }

        var settings = await _brandingSettingsStore.GetAsync(companyPhone, cancellationToken) ??
            BrandingDefaults.CreateDefault(companyPhone, companyName);
        return CreateModelFromBranding(settings);
    }

    private static BrandingViewModel CreateModelFromBranding(BrandingSettings settings)
    {
        var palette = BrandingDefaults.GetPalette(settings.PaletteKey);
        return new BrandingViewModel
        {
            CompanyPhone = settings.StoreId,
            SiteName = settings.SiteName,
            PaletteKey = palette.Key,
            PrimaryColor = palette.PrimaryColor,
            SecondaryColor = palette.SecondaryColor,
            BackgroundColor = palette.BackgroundColor,
            LogoDataUrl = settings.LogoDataUrl
        };
    }

    private static void ApplyInput(BrandingViewModel model, BrandingFormInput input)
    {
        var palette = BrandingDefaults.GetPalette(input.PaletteKey);
        model.SiteName = string.IsNullOrWhiteSpace(input.SiteName)
            ? model.SiteName
            : input.SiteName.Trim();
        model.PaletteKey = palette.Key;
        model.PrimaryColor = palette.PrimaryColor;
        model.SecondaryColor = palette.SecondaryColor;
        model.BackgroundColor = palette.BackgroundColor;
    }

    private static async Task<string?> ReadLogoDataUrlAsync(IFormFile? logo, CancellationToken cancellationToken)
    {
        if (logo is null || logo.Length == 0)
        {
            return null;
        }

        if (logo.Length > MaxLogoBytes)
        {
            throw new InvalidOperationException("A logo deve ter no maximo 512 KB.");
        }

        var mediaType = NormalizeLogoMediaType(logo);
        await using var memory = new MemoryStream();
        await logo.CopyToAsync(memory, cancellationToken);
        return $"data:{mediaType};base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private static string NormalizeLogoMediaType(IFormFile logo)
    {
        var contentType = logo.ContentType.Trim().ToLowerInvariant();
        if (contentType is "image/png" or "image/jpeg" or "image/webp")
        {
            return contentType;
        }

        var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Use uma logo PNG, JPG ou WEBP.")
        };
    }
}
