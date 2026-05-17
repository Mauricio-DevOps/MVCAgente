namespace Mvc.Models;

public sealed class ConversationsViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<WhatsappConversationSummaryResponse> Conversations { get; set; } =
        Array.Empty<WhatsappConversationSummaryResponse>();
}

public sealed record WhatsappConversationSummaryResponse(
    string PhoneNumber,
    string? CustomerId,
    string? CustomerName,
    bool IsAgentEnabled,
    string LastMessage,
    string LastMessageDirection,
    string LastMessageType,
    string LastMessageStatus,
    string LastMessageAtUtc,
    int MessageCount);

public sealed record WhatsappConversationMessageResponse(
    string Id,
    string PhoneNumber,
    string Direction,
    string MessageType,
    string Body,
    string? TwilioMessageSid,
    string? SourceJobId,
    string Status,
    string? Error,
    string CreatedAtUtc);

public sealed record WhatsappContactAgentUpdateRequest(
    string StoreId,
    bool IsAgentEnabled);

public sealed record WhatsappContactAgentResponse(
    string PhoneNumber,
    bool IsAgentEnabled);

public sealed record WhatsappManualMessageRequest(
    string StoreId,
    string Message);

public sealed record ConversationToggleAgentRequest(
    string PhoneNumber,
    bool IsAgentEnabled);

public sealed record ConversationSendMessageRequest(
    string PhoneNumber,
    string Message);
