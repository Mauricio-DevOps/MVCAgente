using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

[Authorize]
[RequireWhatsAppPortalAccess]
public sealed class ConversationsController : Controller
{
    private readonly ApiClient _apiClient;

    public ConversationsController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var companyPhone = GetCompanyPhone();
        if (string.IsNullOrWhiteSpace(companyPhone))
        {
            return BadRequest(new { message = "Login sem telefone de empresa vinculado." });
        }

        try
        {
            var conversations = await _apiClient.GetWhatsappConversationsAsync(companyPhone, cancellationToken);
            return Json(conversations);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Nao foi possivel carregar as conversas." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A API demorou para responder." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Messages(
        [FromQuery] string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var companyPhone = GetCompanyPhone();
        if (string.IsNullOrWhiteSpace(companyPhone) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new { message = "Empresa e telefone sao obrigatorios." });
        }

        try
        {
            var messages = await _apiClient.GetWhatsappConversationMessagesAsync(
                companyPhone,
                phoneNumber.Trim(),
                cancellationToken);
            return Json(messages);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Nao foi possivel carregar as mensagens." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A API demorou para responder." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAgent(
        [FromBody] ConversationToggleAgentRequest request,
        CancellationToken cancellationToken)
    {
        var companyPhone = GetCompanyPhone();
        if (string.IsNullOrWhiteSpace(companyPhone) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { message = "Empresa e telefone sao obrigatorios." });
        }

        try
        {
            var response = await _apiClient.SetWhatsappConversationAgentAsync(
                companyPhone,
                request.PhoneNumber.Trim(),
                request.IsAgentEnabled,
                cancellationToken);
            return Json(response);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Nao foi possivel atualizar o agente." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A API demorou para responder." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(
        [FromBody] ConversationSendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var companyPhone = GetCompanyPhone();
        if (string.IsNullOrWhiteSpace(companyPhone) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Empresa, telefone e mensagem sao obrigatorios." });
        }

        try
        {
            var message = await _apiClient.SendWhatsappConversationMessageAsync(
                companyPhone,
                request.PhoneNumber.Trim(),
                request.Message.Trim(),
                cancellationToken);
            return Json(message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Nao foi possivel enviar a mensagem." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A API demorou para responder." });
        }
    }

    private async Task<ConversationsViewModel> CreateModelAsync(CancellationToken cancellationToken)
    {
        var model = new ConversationsViewModel
        {
            CompanyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa",
            CompanyPhone = GetCompanyPhone()
        };

        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return model;
        }

        try
        {
            model.Conversations = await _apiClient.GetWhatsappConversationsAsync(
                model.CompanyPhone,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar as conversas da API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder as conversas.";
        }

        return model;
    }

    private string GetCompanyPhone()
    {
        return User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty;
    }
}
