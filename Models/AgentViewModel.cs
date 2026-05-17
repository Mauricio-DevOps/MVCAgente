namespace Mvc.Models;

public sealed class AgentViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public IReadOnlyList<ProductResponse> Products { get; set; } = Array.Empty<ProductResponse>();

    public string? SelectedProductId { get; set; }

    public string CampaignMessage { get; set; } = string.Empty;

    public string ReminderMessage { get; set; } = AgentDefaults.CustomerReminderMessage;

    public AgentPersonaSettingsResponse Persona { get; set; } = AgentDefaults.CreateDefaultPersona(string.Empty);

    public AgentFeedbackSettingsResponse FeedbackSettings { get; set; } =
        AgentDefaults.CreateDefaultFeedbackSettings(string.Empty);

    public AgentFeedbackSettingsFormInput FeedbackSettingsForm { get; set; } =
        AgentFeedbackSettingsFormInput.CreateDefault();

    public IReadOnlyList<AgentFeedbackSolicitationResponse> FeedbackSolicitations { get; set; } =
        Array.Empty<AgentFeedbackSolicitationResponse>();

    public AgentProductCampaignPreviewResponse? CampaignPreview { get; set; }

    public AgentAutomatedCampaignFormInput AutomatedCampaignForm { get; set; } =
        AgentAutomatedCampaignFormInput.CreateDefault();

    public IReadOnlyList<AgentAutomatedCampaignResponse> AutomatedCampaigns { get; set; } =
        Array.Empty<AgentAutomatedCampaignResponse>();

    public IReadOnlyList<AgentCustomerRecurrenceResponse> Customers { get; set; } =
        Array.Empty<AgentCustomerRecurrenceResponse>();

    public bool CustomersLoaded { get; set; }

    public AgentSendResultResponse? SendResult { get; set; }
}

public static class AgentDefaults
{
    public const string CustomerReminderMessage =
        "Ola! Verifiquei aqui que ja faz um tempo que voce nao faz pedido com a gente, seu estoque pode estar acabando. Gostaria de fazer um pedido?";

    public static IReadOnlyList<AgentPersonaToneOption> ToneOptions { get; } =
    [
        new(AgentPersonaTones.Amigavel, "Amigavel"),
        new(AgentPersonaTones.Formal, "Formal"),
        new(AgentPersonaTones.Casual, "Casual"),
        new(AgentPersonaTones.Vendedor, "Vendedor"),
        new(AgentPersonaTones.Objetivo, "Objetivo")
    ];

    public static IReadOnlyList<AgentAutomatedCampaignTypeOption> AutomatedCampaignTypeOptions { get; } =
    [
        new(AgentAutomatedCampaignTypes.ProductStock, "Produto por estoque"),
        new(AgentAutomatedCampaignTypes.Recurrence, "Recorrencia"),
        new(AgentAutomatedCampaignTypes.InactiveCustomers, "Clientes inativos")
    ];

    public static IReadOnlyList<AgentFeedbackFormatOption> FeedbackFormatOptions { get; } =
    [
        new(AgentFeedbackFormats.Both, "Texto ou audio"),
        new(AgentFeedbackFormats.Text, "Somente texto"),
        new(AgentFeedbackFormats.Audio, "Somente audio")
    ];

    public static AgentPersonaSettingsResponse CreateDefaultPersona(string storeId)
    {
        return new AgentPersonaSettingsResponse(
            storeId,
            AgentPersonaTones.Amigavel,
            string.Empty,
            Array.Empty<AgentPersonaFaqResponse>());
    }

    public static AgentFeedbackSettingsResponse CreateDefaultFeedbackSettings(string storeId)
    {
        return new AgentFeedbackSettingsResponse(
            storeId,
            IsPostOrderEnabled: false,
            PostOrderDelayMinutes: 60,
            AgentFeedbackFormats.Both,
            "Ola! Seu pedido foi concluido. Pode nos contar como foi sua experiencia? Voce pode responder por texto ou audio.",
            IsPeriodicSurveyEnabled: false,
            PeriodicSurveyDays: 10,
            PeriodicSurveySampleSize: 10,
            LastPeriodicSurveyRunAtUtc: null,
            UpdatedAtUtc: string.Empty);
    }

    public static string BuildCustomerReminderMessage(string? tone)
    {
        return AgentPersonaTones.Normalize(tone) switch
        {
            AgentPersonaTones.Formal =>
                "Ola. Identificamos que ja faz algum tempo desde seu ultimo pedido conosco. Gostaria de registrar um novo pedido?",
            AgentPersonaTones.Casual =>
                "Oi! Faz um tempinho que voce nao pede com a gente. Quer fazer um novo pedido hoje?",
            AgentPersonaTones.Vendedor =>
                "Temos uma oportunidade para repor seu estoque antes que acabe. Posso registrar um novo pedido para voce?",
            AgentPersonaTones.Objetivo =>
                "Faz algum tempo desde seu ultimo pedido. Deseja fazer um novo pedido?",
            _ => CustomerReminderMessage
        };
    }
}

public sealed record AgentPersonaToneOption(string Value, string Label);

public sealed record AgentFeedbackFormatOption(string Value, string Label);

public static class AgentPersonaTones
{
    public const string Amigavel = "AMIGAVEL";
    public const string Formal = "FORMAL";
    public const string Casual = "CASUAL";
    public const string Vendedor = "VENDEDOR";
    public const string Objetivo = "OBJETIVO";

    public static string Normalize(string? tone)
    {
        var normalized = string.IsNullOrWhiteSpace(tone)
            ? Amigavel
            : tone.Trim().ToUpperInvariant();

        return normalized is Formal or Casual or Vendedor or Objetivo or Amigavel
            ? normalized
            : Amigavel;
    }
}

public sealed class AgentPersonaFormInput
{
    public string Tone { get; set; } = AgentPersonaTones.Amigavel;

    public string? CustomInstructions { get; set; }

    public List<AgentPersonaFaqFormInput> Faqs { get; set; } = [];
}

public sealed class AgentPersonaFaqFormInput
{
    public string? Id { get; set; }

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed record ProductResponse(
    string Id,
    string StoreId,
    string Name,
    string? Description,
    string? Type,
    string? Brand,
    decimal RetailPrice,
    decimal? PromotionalPrice,
    decimal WholesalePrice,
    IReadOnlyList<string> Aliases,
    int? StockQuantity,
    int? LowStockThreshold,
    bool IsActive);

public static class AgentAutomatedCampaignTypes
{
    public const string ProductStock = "PRODUCT_STOCK";
    public const string Recurrence = "RECURRENCE";
    public const string InactiveCustomers = "INACTIVE_CUSTOMERS";

    public static string Normalize(string? type)
    {
        var normalized = string.IsNullOrWhiteSpace(type)
            ? ProductStock
            : type.Trim().ToUpperInvariant();

        return normalized is Recurrence or InactiveCustomers or ProductStock
            ? normalized
            : ProductStock;
    }

    public static string ToLabel(string? type)
    {
        return Normalize(type) switch
        {
            Recurrence => "Recorrencia",
            InactiveCustomers => "Clientes inativos",
            _ => "Produto por estoque"
        };
    }
}

public sealed record AgentAutomatedCampaignTypeOption(string Value, string Label);

public sealed class AgentAutomatedCampaignFormInput
{
    public string? Id { get; set; }

    public string Type { get; set; } = AgentAutomatedCampaignTypes.ProductStock;

    public string Name { get; set; } = string.Empty;

    public string? ProductId { get; set; }

    public string Message { get; set; } = AgentDefaults.CustomerReminderMessage;

    public bool IsActive { get; set; }

    public string DailyRunTime { get; set; } = "09:00";

    public int CooldownDays { get; set; } = 7;

    public int InactiveDaysThreshold { get; set; } = 30;

    public static AgentAutomatedCampaignFormInput CreateDefault()
    {
        return new AgentAutomatedCampaignFormInput();
    }
}

public sealed record AgentAutomatedCampaignResponse(
    string Id,
    string StoreId,
    string Type,
    string Name,
    string? ProductId,
    string? ProductName,
    string Message,
    bool IsActive,
    string DailyRunTime,
    int CooldownDays,
    int? InactiveDaysThreshold,
    string? LastRunAtUtc,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    AgentAutomatedCampaignRunResponse? LastRun,
    IReadOnlyList<AgentAutomatedCampaignDeliveryResponse> RecentDeliveries);

public sealed record AgentAutomatedCampaignUpsertRequest(
    string StoreId,
    string? Id,
    string Type,
    string Name,
    string? ProductId,
    string Message,
    bool IsActive,
    string? DailyRunTime,
    int? CooldownDays,
    int? InactiveDaysThreshold);

public sealed record AgentAutomatedCampaignRunResponse(
    string Id,
    string CampaignId,
    string StoreId,
    string StartedAtUtc,
    string CompletedAtUtc,
    int EligibleCount,
    int SkippedCooldownCount,
    int SentCount,
    int FailedCount,
    string? Error,
    IReadOnlyList<AgentAutomatedCampaignDeliveryResponse> Deliveries);

public sealed record AgentAutomatedCampaignDeliveryResponse(
    string Id,
    string CampaignId,
    string RunId,
    string PhoneNumber,
    bool Sent,
    string? Error,
    string CreatedAtUtc);

public static class AgentFeedbackFormats
{
    public const string Text = "TEXT";
    public const string Audio = "AUDIO";
    public const string Both = "BOTH";

    public static string Normalize(string? format)
    {
        var normalized = string.IsNullOrWhiteSpace(format)
            ? Both
            : format.Trim().ToUpperInvariant();

        return normalized is Text or Audio or Both ? normalized : Both;
    }
}

public static class AgentFeedbackCategories
{
    public const string Elogio = "ELOGIO";
    public const string Reclamacao = "RECLAMACAO";
    public const string Opiniao = "OPINIAO";
    public const string Sugestao = "SUGESTAO";
    public const string Outro = "OUTRO";
    public const string Indefinido = "INDEFINIDO";

    public static string Normalize(string? category)
    {
        var normalized = string.IsNullOrWhiteSpace(category)
            ? Indefinido
            : category.Trim().ToUpperInvariant();

        return normalized is Elogio or Reclamacao or Opiniao or Sugestao or Outro or Indefinido
            ? normalized
            : Indefinido;
    }
}

public static class AgentFeedbackSentiments
{
    public const string Positivo = "POSITIVO";
    public const string Neutro = "NEUTRO";
    public const string Negativo = "NEGATIVO";
    public const string Indefinido = "INDEFINIDO";

    public static string Normalize(string? sentiment)
    {
        var normalized = string.IsNullOrWhiteSpace(sentiment)
            ? Indefinido
            : sentiment.Trim().ToUpperInvariant();

        return normalized is Positivo or Neutro or Negativo or Indefinido
            ? normalized
            : Indefinido;
    }
}

public static class AgentFeedbackCustomerClassifications
{
    public const string Promotor = "PROMOTOR";
    public const string Neutro = "NEUTRO";
    public const string Detrator = "DETRATOR";
    public const string Indefinido = "INDEFINIDO";

    public static string Normalize(string? classification)
    {
        var normalized = string.IsNullOrWhiteSpace(classification)
            ? Indefinido
            : classification.Trim().ToUpperInvariant();

        return normalized is Promotor or Neutro or Detrator or Indefinido
            ? normalized
            : Indefinido;
    }
}

public sealed class AgentFeedbackSettingsFormInput
{
    public bool IsPostOrderEnabled { get; set; }

    public int PostOrderDelayMinutes { get; set; } = 60;

    public string AcceptedFormat { get; set; } = AgentFeedbackFormats.Both;

    public string RequestMessage { get; set; } =
        "Ola! Seu pedido foi concluido. Pode nos contar como foi sua experiencia? Voce pode responder por texto ou audio.";

    public bool IsPeriodicSurveyEnabled { get; set; }

    public int PeriodicSurveyDays { get; set; } = 10;

    public int PeriodicSurveySampleSize { get; set; } = 10;

    public static AgentFeedbackSettingsFormInput CreateDefault()
    {
        return new AgentFeedbackSettingsFormInput();
    }

    public static AgentFeedbackSettingsFormInput FromResponse(AgentFeedbackSettingsResponse response)
    {
        return new AgentFeedbackSettingsFormInput
        {
            IsPostOrderEnabled = response.IsPostOrderEnabled,
            PostOrderDelayMinutes = response.PostOrderDelayMinutes,
            AcceptedFormat = AgentFeedbackFormats.Normalize(response.AcceptedFormat),
            RequestMessage = response.RequestMessage,
            IsPeriodicSurveyEnabled = response.IsPeriodicSurveyEnabled,
            PeriodicSurveyDays = response.PeriodicSurveyDays,
            PeriodicSurveySampleSize = response.PeriodicSurveySampleSize
        };
    }
}

public sealed record AgentFeedbackSettingsResponse(
    string StoreId,
    bool IsPostOrderEnabled,
    int PostOrderDelayMinutes,
    string AcceptedFormat,
    string RequestMessage,
    bool IsPeriodicSurveyEnabled,
    int PeriodicSurveyDays,
    int PeriodicSurveySampleSize,
    string? LastPeriodicSurveyRunAtUtc,
    string UpdatedAtUtc);

public sealed record AgentFeedbackSettingsUpsertRequest(
    string StoreId,
    bool IsPostOrderEnabled,
    int? PostOrderDelayMinutes,
    string? AcceptedFormat,
    string? RequestMessage,
    bool IsPeriodicSurveyEnabled,
    int? PeriodicSurveyDays,
    int? PeriodicSurveySampleSize);

public sealed record AgentFeedbackSolicitationResponse(
    string Id,
    string StoreId,
    string? OrderId,
    string PhoneNumber,
    string Kind,
    string Status,
    string Message,
    string DueAtUtc,
    string? SentAtUtc,
    string? RespondedAtUtc,
    string? LastError,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    AgentFeedbackResponseResponse? Response);

public sealed record AgentFeedbackResponseResponse(
    string Id,
    string SolicitationId,
    string StoreId,
    string PhoneNumber,
    string ResponseType,
    string? Text,
    string? MediaUrl,
    string? MediaContentType,
    string? Category,
    string? Sentiment,
    string? CustomerClassification,
    int? Score,
    string? Summary,
    string? AnalyzedAtUtc,
    string? PromptResponseId,
    string? ConversationId,
    string? AiOutputJson,
    string CreatedAtUtc);

public sealed record AgentCampaignCustomerResponse(
    string PhoneNumber,
    string LastOrderAtUtc,
    int TotalOrders);

public sealed record AgentProductCampaignPreviewResponse(
    ProductResponse Product,
    string SuggestedMessage,
    IReadOnlyList<AgentCampaignCustomerResponse> Customers);

public sealed record AgentProductCampaignSendRequest(
    string StoreId,
    string ProductId,
    string Message);

public sealed record AgentCustomerRecurrenceResponse(
    string PhoneNumber,
    string LastOrderAtUtc,
    int TotalOrders,
    decimal? AverageDaysBetweenOrders,
    decimal DaysSinceLastOrder,
    bool IsOverdue);

public sealed record AgentCustomerReminderSendRequest(
    string StoreId,
    string PhoneNumber,
    string Message);

public sealed record AgentSendResultResponse(
    int SentCount,
    int FailedCount,
    IReadOnlyList<AgentSendResultItemResponse> Results);

public sealed record AgentSendResultItemResponse(
    string PhoneNumber,
    bool Sent,
    string? Error);

public sealed record AgentPersonaSettingsResponse(
    string StoreId,
    string Tone,
    string CustomInstructions,
    IReadOnlyList<AgentPersonaFaqResponse> Faqs);

public sealed record AgentPersonaSettingsUpsertRequest(
    string StoreId,
    string Tone,
    string? CustomInstructions,
    IReadOnlyList<AgentPersonaFaqUpsert> Faqs);

public sealed record AgentPersonaFaqResponse(
    string Id,
    string Question,
    string Answer,
    bool IsActive,
    int SortOrder);

public sealed record AgentPersonaFaqUpsert(
    string? Id,
    string Question,
    string Answer,
    bool IsActive,
    int SortOrder);
