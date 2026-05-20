using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

[Authorize]
[RequireWhatsAppPortalAccess]
public sealed class DashboardController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly BrandingSettingsStore _brandingSettingsStore;

    public DashboardController(ApiClient apiClient, BrandingSettingsStore brandingSettingsStore)
    {
        _apiClient = apiClient;
        _brandingSettingsStore = brandingSettingsStore;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa";
        var companyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty;
        var model = new DashboardViewModel
        {
            CompanyName = companyName,
            CompanyPhone = companyPhone
        };

        if (string.IsNullOrWhiteSpace(companyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return View(model);
        }

        var branding = await _brandingSettingsStore.GetAsync(companyPhone, cancellationToken);
        if (branding is not null)
        {
            model.CompanyName = branding.SiteName;
        }

        try
        {
            model.Dashboard = await _apiClient.GetDashboardAsync(companyPhone, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar o dashboard da API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao dashboard.";
        }

        return View(model);
    }
}
