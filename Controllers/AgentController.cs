using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

[Authorize]
[RequireWhatsAppPortalAccess]
public sealed class AgentController : Controller
{
    private readonly ApiClient _apiClient;

    public AgentController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await CreateModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePersona(
        AgentPersonaFormInput input,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        var faqs = NormalizePersonaFaqs(input.Faqs, out var validationError);
        if (validationError is not null)
        {
            model.Persona = new AgentPersonaSettingsResponse(
                model.CompanyPhone,
                AgentPersonaTones.Normalize(input.Tone),
                input.CustomInstructions?.Trim() ?? string.Empty,
                faqs.Select(faq => new AgentPersonaFaqResponse(
                    faq.Id ?? string.Empty,
                    faq.Question,
                    faq.Answer,
                    faq.IsActive,
                    faq.SortOrder)).ToArray());
            model.ErrorMessage = validationError;
            return View("Index", model);
        }

        try
        {
            model.Persona = await _apiClient.SaveAgentPersonaAsync(
                new AgentPersonaSettingsUpsertRequest(
                    model.CompanyPhone,
                    AgentPersonaTones.Normalize(input.Tone),
                    input.CustomInstructions?.Trim(),
                    faqs),
                cancellationToken);
            model.ReminderMessage = AgentDefaults.BuildCustomerReminderMessage(model.Persona.Tone);
            model.AutomatedCampaignForm.Message = BuildAutomatedCampaignMessage(model.Persona.Tone);
            model.SuccessMessage = "Persona do agente salva.";
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar a persona do agente.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para salvar a persona do agente.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationSettings(
        [Bind(Prefix = "NotificationSettingsForm")] AgentNotificationSettingsFormInput input,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.NotificationSettingsForm = input;

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        try
        {
            model.NotificationSettings = await _apiClient.SaveAgentNotificationSettingsAsync(
                new AgentNotificationSettingsUpsertRequest(
                    model.CompanyPhone,
                    input.StaffNotificationPhoneNumber?.Trim()),
                cancellationToken);
            model.NotificationSettingsForm = AgentNotificationSettingsFormInput.FromResponse(model.NotificationSettings);
            model.SuccessMessage = "Telefone responsavel salvo.";
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar o telefone responsavel.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para salvar o telefone responsavel.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFeedbackSettings(
        [Bind(Prefix = "FeedbackSettingsForm")] AgentFeedbackSettingsFormInput input,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.FeedbackSettingsForm = input;

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        var request = NormalizeFeedbackSettingsInput(model.CompanyPhone, input, out var validationError);
        if (validationError is not null || request is null)
        {
            model.ErrorMessage = validationError ?? "Configuracao de feedback invalida.";
            return View("Index", model);
        }

        try
        {
            model.FeedbackSettings = await _apiClient.SaveAgentFeedbackSettingsAsync(request, cancellationToken);
            model.FeedbackSettingsForm = AgentFeedbackSettingsFormInput.FromResponse(model.FeedbackSettings);
            model.SuccessMessage = "Configuracao de feedback automatico salva.";
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar a configuracao de feedback.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para salvar a configuracao de feedback.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFeedbackSolicitation(
        [FromForm] string? solicitationId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(solicitationId))
        {
            model.ErrorMessage = "Solicitacao de feedback invalida.";
            return View("Index", model);
        }

        try
        {
            await _apiClient.SendAgentFeedbackSolicitationAsync(
                model.CompanyPhone,
                solicitationId.Trim(),
                cancellationToken);
            model.SuccessMessage = "Solicitacao de feedback enviada.";
            await LoadFeedbackSolicitationsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel enviar a solicitacao de feedback.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para enviar a solicitacao de feedback.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewCampaign(
        [FromForm] string? selectedProductId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.SelectedProductId = selectedProductId?.Trim();

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(model.SelectedProductId))
        {
            model.ErrorMessage = "Selecione um produto para gerar a previa.";
            return View("Index", model);
        }

        try
        {
            model.CampaignPreview = await _apiClient.GetAgentProductCampaignPreviewAsync(
                model.CompanyPhone,
                model.SelectedProductId,
                cancellationToken);
            model.CampaignMessage = model.CampaignPreview?.SuggestedMessage ?? string.Empty;
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel gerar a previa da campanha.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder a previa da campanha.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCampaign(
        [FromForm] string? selectedProductId,
        [FromForm] string? campaignMessage,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.SelectedProductId = selectedProductId?.Trim();
        model.CampaignMessage = campaignMessage?.Trim() ?? string.Empty;

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(model.SelectedProductId) ||
            string.IsNullOrWhiteSpace(model.CampaignMessage))
        {
            model.ErrorMessage = "Selecione um produto e informe a mensagem da campanha.";
            return View("Index", model);
        }

        try
        {
            model.SendResult = await _apiClient.SendAgentProductCampaignAsync(
                model.CompanyPhone,
                model.SelectedProductId,
                model.CampaignMessage,
                cancellationToken);
            model.SuccessMessage = BuildSendMessage("Campanha enviada", model.SendResult);
            try
            {
                model.CampaignPreview = await _apiClient.GetAgentProductCampaignPreviewAsync(
                    model.CompanyPhone,
                    model.SelectedProductId,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel enviar a campanha pela API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao envio da campanha.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsultCustomers(CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        await LoadCustomersAsync(model, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(
        [FromForm] string? phoneNumber,
        [FromForm] string[]? phoneNumbers,
        [FromForm] string? reminderMessage,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.ReminderMessage = string.IsNullOrWhiteSpace(reminderMessage)
            ? AgentDefaults.BuildCustomerReminderMessage(model.Persona.Tone)
            : reminderMessage.Trim();

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        IReadOnlyList<string> selectedPhoneNumbers = string.IsNullOrWhiteSpace(phoneNumber)
            ? NormalizePhoneNumbers(phoneNumbers)
            : new[] { phoneNumber.Trim() };

        if (selectedPhoneNumbers.Count == 0 || string.IsNullOrWhiteSpace(model.ReminderMessage))
        {
            model.ErrorMessage = "Selecione ao menos um cliente e informe a mensagem de lembrete.";
            await LoadCustomersAsync(model, cancellationToken);
            return View("Index", model);
        }

        model.SendResult = await SendCustomerRemindersAsync(
            model.CompanyPhone,
            selectedPhoneNumbers,
            model.ReminderMessage,
            cancellationToken);
        model.SuccessMessage = BuildSendMessage("Lembrete enviado", model.SendResult);

        await LoadCustomersAsync(model, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAutomatedCampaign(
        [Bind(Prefix = "AutomatedCampaignForm")] AgentAutomatedCampaignFormInput input,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.AutomatedCampaignForm = input;

        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        var request = NormalizeAutomatedCampaignInput(model.CompanyPhone, input, out var validationError);
        if (validationError is not null || request is null)
        {
            model.ErrorMessage = validationError ?? "Campanha automatizada invalida.";
            return View("Index", model);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                await _apiClient.CreateAgentAutomatedCampaignAsync(request, cancellationToken);
                model.SuccessMessage = "Campanha automatizada criada. Ela comeca inativa, a menos que voce marque como ativa.";
            }
            else
            {
                await _apiClient.UpdateAgentAutomatedCampaignAsync(input.Id.Trim(), request, cancellationToken);
                model.SuccessMessage = "Campanha automatizada atualizada.";
            }

            model.AutomatedCampaignForm = AgentAutomatedCampaignFormInput.CreateDefault();
            model.AutomatedCampaignForm.Message = BuildAutomatedCampaignMessage(model.Persona.Tone);
            await LoadAutomatedCampaignsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar a campanha automatizada.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para salvar a campanha automatizada.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAutomatedCampaign(
        [FromForm] string? campaignId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(campaignId))
        {
            model.ErrorMessage = "Campanha automatizada invalida.";
            return View("Index", model);
        }

        try
        {
            var run = await _apiClient.RunAgentAutomatedCampaignAsync(
                model.CompanyPhone,
                campaignId.Trim(),
                cancellationToken);

            model.SuccessMessage = run is null
                ? "Campanha nao encontrada."
                : $"Execucao concluida: {run.SentCount} enviado(s), {run.FailedCount} falha(s), {run.SkippedCooldownCount} em cooldown.";
            await LoadAutomatedCampaignsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel executar a campanha automatizada.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para executar a campanha automatizada.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAutomatedCampaign(
        [FromForm] string? campaignId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseAgent(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(campaignId))
        {
            model.ErrorMessage = "Campanha automatizada invalida.";
            return View("Index", model);
        }

        try
        {
            await _apiClient.DeleteAgentAutomatedCampaignAsync(
                model.CompanyPhone,
                campaignId.Trim(),
                cancellationToken);
            model.SuccessMessage = "Campanha automatizada removida.";
            await LoadAutomatedCampaignsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel remover a campanha automatizada.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para remover a campanha automatizada.";
        }

        return View("Index", model);
    }

    private async Task<AgentViewModel> CreateModelAsync(CancellationToken cancellationToken)
    {
        var model = new AgentViewModel
        {
            CompanyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa",
            CompanyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return model;
        }

        try
        {
            model.Persona = await _apiClient.GetAgentPersonaAsync(model.CompanyPhone, cancellationToken);
            model.NotificationSettings = await _apiClient.GetAgentNotificationSettingsAsync(model.CompanyPhone, cancellationToken);
            model.NotificationSettingsForm = AgentNotificationSettingsFormInput.FromResponse(model.NotificationSettings);
            model.FeedbackSettings = await _apiClient.GetAgentFeedbackSettingsAsync(model.CompanyPhone, cancellationToken);
            model.FeedbackSettingsForm = AgentFeedbackSettingsFormInput.FromResponse(model.FeedbackSettings);
            model.FeedbackSolicitations = await _apiClient.GetAgentFeedbackSolicitationsAsync(model.CompanyPhone, cancellationToken);
            model.ReminderMessage = AgentDefaults.BuildCustomerReminderMessage(model.Persona.Tone);
            model.AutomatedCampaignForm.Message = BuildAutomatedCampaignMessage(model.Persona.Tone);
            model.Products = await _apiClient.GetProductsAsync(model.CompanyPhone, cancellationToken);
            model.AutomatedCampaigns = await _apiClient.GetAgentAutomatedCampaignsAsync(model.CompanyPhone, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar os dados do agente pela API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder aos dados do agente.";
        }

        return model;
    }

    private async Task LoadFeedbackSolicitationsAsync(AgentViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.FeedbackSolicitations = await _apiClient.GetAgentFeedbackSolicitationsAsync(
                model.CompanyPhone,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar o historico de feedback.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder o historico de feedback.";
        }
    }

    private async Task LoadAutomatedCampaignsAsync(AgentViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.AutomatedCampaigns = await _apiClient.GetAgentAutomatedCampaignsAsync(
                model.CompanyPhone,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar as campanhas automatizadas.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder as campanhas automatizadas.";
        }
    }

    private async Task LoadCustomersAsync(AgentViewModel model, CancellationToken cancellationToken)
    {
        model.CustomersLoaded = true;

        try
        {
            model.Customers = await _apiClient.GetAgentCustomersAsync(model.CompanyPhone, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel consultar a recorrencia dos clientes.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder a consulta de clientes.";
        }
    }

    private async Task<AgentSendResultResponse> SendCustomerRemindersAsync(
        string storeId,
        IReadOnlyList<string> phoneNumbers,
        string message,
        CancellationToken cancellationToken)
    {
        var results = new List<AgentSendResultItemResponse>();

        foreach (var phoneNumber in phoneNumbers)
        {
            try
            {
                var sendResult = await _apiClient.SendAgentCustomerReminderAsync(
                    storeId,
                    phoneNumber,
                    message,
                    cancellationToken);
                results.AddRange(sendResult.Results);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                results.Add(new AgentSendResultItemResponse(
                    phoneNumber,
                    Sent: false,
                    "Nao foi possivel enviar o lembrete pela API."));
            }
            catch (TaskCanceledException)
            {
                results.Add(new AgentSendResultItemResponse(
                    phoneNumber,
                    Sent: false,
                    "A API demorou para responder ao envio do lembrete."));
            }
        }

        return new AgentSendResultResponse(
            results.Count(result => result.Sent),
            results.Count(result => !result.Sent),
            results);
    }

    private static IReadOnlyList<string> NormalizePhoneNumbers(string[]? phoneNumbers)
    {
        if (phoneNumbers is null || phoneNumbers.Length == 0)
        {
            return Array.Empty<string>();
        }

        return phoneNumbers
            .Where(phoneNumber => !string.IsNullOrWhiteSpace(phoneNumber))
            .Select(phoneNumber => phoneNumber.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<AgentPersonaFaqUpsert> NormalizePersonaFaqs(
        IReadOnlyList<AgentPersonaFaqFormInput>? faqs,
        out string? validationError)
    {
        validationError = null;
        if (faqs is null || faqs.Count == 0)
        {
            return Array.Empty<AgentPersonaFaqUpsert>();
        }

        var normalized = new List<AgentPersonaFaqUpsert>();
        var sortOrder = 1;
        foreach (var faq in faqs)
        {
            var question = faq.Question?.Trim();
            var answer = faq.Answer?.Trim();
            var hasQuestion = !string.IsNullOrWhiteSpace(question);
            var hasAnswer = !string.IsNullOrWhiteSpace(answer);

            if (!hasQuestion && !hasAnswer)
            {
                continue;
            }

            if (hasQuestion != hasAnswer)
            {
                validationError = "Cada FAQ precisa ter pergunta e resposta.";
                return normalized;
            }

            normalized.Add(new AgentPersonaFaqUpsert(
                string.IsNullOrWhiteSpace(faq.Id) ? null : faq.Id.Trim(),
                question!,
                answer!,
                faq.IsActive,
                sortOrder++));
        }

        return normalized;
    }

    private static AgentAutomatedCampaignUpsertRequest? NormalizeAutomatedCampaignInput(
        string storeId,
        AgentAutomatedCampaignFormInput input,
        out string? validationError)
    {
        validationError = null;
        var type = AgentAutomatedCampaignTypes.Normalize(input.Type);
        var name = input.Name?.Trim() ?? string.Empty;
        var message = input.Message?.Trim() ?? string.Empty;
        var productId = string.IsNullOrWhiteSpace(input.ProductId) ? null : input.ProductId.Trim();
        var dailyRunTime = string.IsNullOrWhiteSpace(input.DailyRunTime)
            ? "09:00"
            : input.DailyRunTime.Trim();
        var cooldownDays = input.CooldownDays <= 0 ? 7 : input.CooldownDays;
        var inactiveDays = input.InactiveDaysThreshold <= 0 ? 30 : input.InactiveDaysThreshold;

        if (string.IsNullOrWhiteSpace(name))
        {
            validationError = "Informe o nome da campanha automatizada.";
            return null;
        }

        if (type == AgentAutomatedCampaignTypes.ProductStock && string.IsNullOrWhiteSpace(productId))
        {
            validationError = "Campanha por estoque precisa de um produto.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            validationError = "Informe a mensagem da campanha automatizada.";
            return null;
        }

        if (!TimeOnly.TryParse(dailyRunTime, out _))
        {
            validationError = "Informe um horario diario valido.";
            return null;
        }

        return new AgentAutomatedCampaignUpsertRequest(
            storeId,
            string.IsNullOrWhiteSpace(input.Id) ? null : input.Id.Trim(),
            type,
            name,
            type == AgentAutomatedCampaignTypes.ProductStock ? productId : null,
            message,
            input.IsActive,
            dailyRunTime,
            cooldownDays,
            type == AgentAutomatedCampaignTypes.InactiveCustomers ? inactiveDays : null);
    }

    private static AgentFeedbackSettingsUpsertRequest? NormalizeFeedbackSettingsInput(
        string storeId,
        AgentFeedbackSettingsFormInput input,
        out string? validationError)
    {
        validationError = null;
        var delay = input.PostOrderDelayMinutes <= 0 ? 60 : input.PostOrderDelayMinutes;
        var periodicDays = input.PeriodicSurveyDays <= 0 ? 10 : input.PeriodicSurveyDays;
        var sampleSize = input.PeriodicSurveySampleSize <= 0 ? 10 : input.PeriodicSurveySampleSize;
        var message = input.RequestMessage?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            validationError = "Informe a mensagem de solicitacao de feedback.";
            return null;
        }

        return new AgentFeedbackSettingsUpsertRequest(
            storeId,
            input.IsPostOrderEnabled,
            delay,
            AgentFeedbackFormats.Normalize(input.AcceptedFormat),
            message,
            input.IsPeriodicSurveyEnabled,
            periodicDays,
            sampleSize);
    }

    private static bool CanUseAgent(AgentViewModel model)
    {
        return string.IsNullOrWhiteSpace(model.ErrorMessage) &&
            !string.IsNullOrWhiteSpace(model.CompanyPhone);
    }

    private static string BuildSendMessage(string prefix, AgentSendResultResponse result)
    {
        return $"{prefix}: {result.SentCount} enviado(s), {result.FailedCount} falha(s).";
    }

    private static string BuildAutomatedCampaignMessage(string? tone)
    {
        return AgentPersonaTones.Normalize(tone) switch
        {
            AgentPersonaTones.Formal =>
                "Ola. Temos uma oportunidade de novo pedido para voce. Caso deseje, podemos registrar sua solicitacao agora.",
            AgentPersonaTones.Casual =>
                "Oi! Passando para avisar que temos uma boa oportunidade para voce pedir de novo. Quer fazer um pedido?",
            AgentPersonaTones.Vendedor =>
                "Oferta especial para voce fazer um novo pedido hoje. Posso separar agora?",
            AgentPersonaTones.Objetivo =>
                "Temos uma oportunidade de novo pedido. Deseja comprar agora?",
            _ =>
                "Ola! Temos uma oportunidade especial para voce pedir de novo com a gente. Gostaria de fazer um pedido?"
        };
    }
}
