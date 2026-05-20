using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    private readonly ApiClient _apiClient;
    private readonly RestaurantMenuApiClient _restaurantMenuApiClient;

    public PersonalizarController(
        ApiClient apiClient,
        RestaurantMenuApiClient restaurantMenuApiClient)
    {
        _apiClient = apiClient;
        _restaurantMenuApiClient = restaurantMenuApiClient;
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

        var siteName = input.SiteName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(siteName))
        {
            model.ErrorMessage = "Informe o nome do site.";
            ApplyInput(model, input);
            return View(model);
        }

        if (siteName.Length > 120)
        {
            model.ErrorMessage = "O nome do site deve ter no maximo 120 caracteres.";
            ApplyInput(model, input);
            return View(model);
        }

        string? logoDataUrl = null;
        try
        {
            logoDataUrl = await ReadLogoDataUrlAsync(logo, cancellationToken);
        }
        catch (InvalidOperationException error)
        {
            model.ErrorMessage = error.Message;
            ApplyInput(model, input);
            return View(model);
        }

        try
        {
            var saved = await _apiClient.SaveBrandingSettingsAsync(
                new BrandingSettingsUpsertRequest(
                    model.CompanyPhone,
                    siteName,
                    input.PaletteKey,
                    logoDataUrl,
                    input.RemoveLogo),
                cancellationToken);

            await RefreshCompanyNameClaimAsync(saved.SiteName);
            model = CreateModelFromBranding(saved);
            model.SuccessMessage = "Personalizacao salva.";

            try
            {
                await _restaurantMenuApiClient.SyncBrandingAsync(
                    new BrandingSyncRequest(
                        saved.StoreId,
                        saved.SiteName,
                        saved.PaletteKey,
                        saved.PrimaryColor,
                        saved.SecondaryColor,
                        saved.BackgroundColor,
                        saved.MenuTheme,
                        saved.MenuMode,
                        saved.LogoDataUrl,
                        input.RemoveLogo),
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                model.WarningMessage = "Personalizacao salva no WhatsApp, mas nao foi possivel sincronizar o cardapio publico.";
            }
            catch (TaskCanceledException)
            {
                model.WarningMessage = "Personalizacao salva no WhatsApp, mas o cardapio publico demorou para responder.";
            }
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar a personalizacao pela API.";
            ApplyInput(model, input);
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para salvar a personalizacao.";
            ApplyInput(model, input);
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

        try
        {
            var settings = await _apiClient.GetBrandingSettingsAsync(companyPhone, cancellationToken) ??
                BrandingDefaults.CreateDefault(companyPhone, companyName);
            return CreateModelFromBranding(settings);
        }
        catch (HttpRequestException)
        {
            var model = CreateModelFromBranding(BrandingDefaults.CreateDefault(companyPhone, companyName));
            model.ErrorMessage = "Nao foi possivel carregar a personalizacao pela API.";
            return model;
        }
        catch (TaskCanceledException)
        {
            var model = CreateModelFromBranding(BrandingDefaults.CreateDefault(companyPhone, companyName));
            model.ErrorMessage = "A API demorou para carregar a personalizacao.";
            return model;
        }
    }

    private static BrandingViewModel CreateModelFromBranding(BrandingSettingsResponse settings)
    {
        var palette = BrandingDefaults.GetPalette(settings.PaletteKey);
        return new BrandingViewModel
        {
            CompanyPhone = settings.StoreId,
            SiteName = settings.SiteName,
            PaletteKey = palette.Key,
            PrimaryColor = settings.PrimaryColor,
            SecondaryColor = settings.SecondaryColor,
            BackgroundColor = settings.BackgroundColor,
            LogoDataUrl = settings.LogoDataUrl
        };
    }

    private static void ApplyInput(BrandingViewModel model, BrandingFormInput input)
    {
        var palette = BrandingDefaults.GetPalette(input.PaletteKey);
        model.SiteName = input.SiteName?.Trim() ?? model.SiteName;
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

        var extension = Path.GetExtension(logo.FileName);
        var mediaType = extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new InvalidOperationException("Use uma logo PNG, JPG ou WEBP.");
        }

        await using var memory = new MemoryStream();
        await logo.CopyToAsync(memory, cancellationToken);
        return $"data:{mediaType};base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private async Task RefreshCompanyNameClaimAsync(string siteName)
    {
        var claims = User.Claims
            .Where(claim => claim.Type != AuthClaims.CompanyName)
            .Select(claim => new Claim(claim.Type, claim.Value))
            .ToList();
        claims.Add(new Claim(AuthClaims.CompanyName, siteName));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
