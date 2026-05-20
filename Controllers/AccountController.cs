using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

public sealed class AccountController : Controller
{
    private readonly SsoTokenService _ssoTokenService;
    private readonly ExternalUrlResolver _externalUrlResolver;
    private readonly ApiClient _apiClient;
    private readonly RestaurantAuthClient _restaurantAuthClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SsoTokenService ssoTokenService,
        ExternalUrlResolver externalUrlResolver,
        ApiClient apiClient,
        RestaurantAuthClient restaurantAuthClient,
        ILogger<AccountController> logger)
    {
        _ssoTokenService = ssoTokenService;
        _externalUrlResolver = externalUrlResolver;
        _apiClient = apiClient;
        _restaurantAuthClient = restaurantAuthClient;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var target = PortalAccess.NormalizeWhatsAppTargetOrDefault(returnUrl);
        if (User.Identity?.IsAuthenticated == true)
        {
            if (PortalAccess.HasWhatsAppAccess(User))
            {
                return LocalRedirect(target);
            }

            return RedirectToAction(nameof(AccessDenied));
        }

        ViewData["ReturnUrl"] = target;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ActionName("Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost(
        LoginViewModel input,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var target = PortalAccess.NormalizeWhatsAppTargetOrDefault(returnUrl);
        ViewData["ReturnUrl"] = target;

        if (!ModelState.IsValid)
        {
            return View("Login", input);
        }

        var hadLoginServiceFailure = false;

        try
        {
            var company = await _apiClient.LoginAsync(input.Username, input.Password, cancellationToken);
            if (company is not null)
            {
                await SignInWhatsAppCompanyAsync(company, accessMode: "SoWhatsApp");
                return LocalRedirect(target);
            }
        }
        catch (HttpRequestException error)
        {
            hadLoginServiceFailure = true;
            _logger.LogWarning(error, "WhatsApp login service failed for username {Username}.", input.Username);
        }
        catch (TaskCanceledException error)
        {
            hadLoginServiceFailure = true;
            _logger.LogWarning(error, "WhatsApp login service timed out for username {Username}.", input.Username);
        }

        try
        {
            var restaurantLogin = await _restaurantAuthClient.LoginAsync(input.Username, input.Password, cancellationToken);
            if (restaurantLogin is not null)
            {
                await SignInRestaurantWhatsAppAsync(restaurantLogin);
                return LocalRedirect(target);
            }
        }
        catch (HttpRequestException error)
        {
            hadLoginServiceFailure = true;
            _logger.LogWarning(error, "Restaurant login service failed for username {Username}.", input.Username);
        }
        catch (TaskCanceledException error)
        {
            hadLoginServiceFailure = true;
            _logger.LogWarning(error, "Restaurant login service timed out for username {Username}.", input.Username);
        }

        input.ErrorMessage = hadLoginServiceFailure
            ? "Nao foi possivel conectar no servico de login."
            : "Usuario ou senha invalidos.";
        return View("Login", input);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Sso(string? token)
    {
        if (!_ssoTokenService.TryValidate(token, out var payload) ||
            !PortalAccess.TryNormalizeWhatsAppTarget(payload.TargetPath, out var normalizedTarget))
        {
            return RedirectToAction(nameof(Login));
        }

        var accessMode = PortalAccess.NormalizeAccessMode(payload.AccessMode);
        if (!PortalAccess.HasWhatsAppAccess(accessMode))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return PortalAccess.HasRestaurantAccess(accessMode)
                ? Redirect(BuildRestaurantUrl("/restaurante"))
                : Redirect(BuildCentralLoginUrl(null));
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        await SignInAsync(
            payload.RestaurantId,
            payload.CompanyPhone,
            payload.RestaurantName,
            payload.CompanyPhone,
            accessMode);

        return LocalRedirect(normalizedTarget);
    }

    [Authorize]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        if (PortalAccess.HasRestaurantAccess(User))
        {
            return Redirect(BuildRestaurantUrl("/restaurante"));
        }

        return Redirect(BuildCentralLoginUrl(null));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var hasRestaurantAccess = PortalAccess.HasRestaurantAccess(User);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return hasRestaurantAccess
            ? Redirect(BuildRestaurantUrl("/logout-remote"))
            : RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> LogoutRemote()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(BuildRestaurantUrl("/login"));
    }

    private string BuildCentralLoginUrl(string? target)
    {
        var bridgePath = $"/sso/whatsapp?target={Uri.EscapeDataString(PortalAccess.NormalizeWhatsAppTargetOrDefault(target))}";
        return BuildRestaurantUrl(bridgePath);
    }

    private string BuildRestaurantUrl(string localPath)
    {
        return _externalUrlResolver.BuildRestaurantUrl(localPath);
    }

    private Task SignInWhatsAppCompanyAsync(CompanyLoginResponse company, string accessMode)
    {
        return SignInAsync(
            company.CompanyId,
            company.Username,
            company.CompanyName,
            company.CompanyPhone,
            accessMode);
    }

    private Task SignInRestaurantWhatsAppAsync(RestaurantWhatsAppLoginResponse login)
    {
        return SignInAsync(
            login.CompanyId,
            login.Username,
            login.CompanyName,
            login.CompanyPhone,
            accessMode: "SoWhatsApp");
    }

    private async Task SignInAsync(
        string companyId,
        string username,
        string companyName,
        string companyPhone,
        string accessMode)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, companyId),
            new(ClaimTypes.Name, username),
            new(AuthClaims.CompanyId, companyId),
            new(AuthClaims.CompanyName, companyName),
            new(AuthClaims.CompanyPhone, companyPhone),
            new(AuthClaims.RestaurantAccessMode, PortalAccess.NormalizeAccessMode(accessMode))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
