using System.Net;
using System.Net.Http.Json;
using Mvc.Models;

namespace Mvc.Services;

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CompanyLoginResponse?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompanyLoginResponse>(cancellationToken);
    }

    public async Task<DashboardResponse?> GetDashboardAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<DashboardResponse>(
            $"/api/admin/dashboard?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ProductResponse>>(
            $"/api/admin/products?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<ProductResponse>();
    }

    public async Task<ProductResponse?> CreateProductAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/products",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
    }

    public async Task<ProductResponse?> UpdateProductAsync(
        string productId,
        ProductUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/admin/products/{Uri.EscapeDataString(productId)}",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
    }

    public async Task<ProductImportResponse> ImportProductsAsync(
        ProductImportRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/products/import",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductImportResponse>(cancellationToken) ??
            new ProductImportResponse(0, 0, 0, Array.Empty<ProductImportErrorViewModel>());
    }

    public async Task InactivateProductAsync(
        string storeId,
        string productId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.DeleteAsync(
            $"/api/admin/products/{Uri.EscapeDataString(productId)}?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CustomerResponse>> GetCustomersAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<CustomerResponse>>(
            $"/api/admin/customers?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<CustomerResponse>();
    }

    public async Task<CustomerResponse?> CreateCustomerAsync(
        CustomerUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/customers",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);
    }

    public async Task<CustomerResponse?> UpdateCustomerAsync(
        string customerId,
        CustomerUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/admin/customers/{Uri.EscapeDataString(customerId)}",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);
    }

    public async Task DeleteCustomerAsync(
        string storeId,
        string customerId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.DeleteAsync(
            $"/api/admin/customers/{Uri.EscapeDataString(customerId)}?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<CustomerImportResponse> ImportCustomersAsync(
        CustomerImportRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/customers/import",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerImportResponse>(cancellationToken) ??
            new CustomerImportResponse(0, 0, 0, Array.Empty<CustomerImportErrorViewModel>());
    }

    public async Task<IReadOnlyList<WhatsappConversationSummaryResponse>> GetWhatsappConversationsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<WhatsappConversationSummaryResponse>>(
            $"/api/admin/whatsapp/conversations?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<WhatsappConversationSummaryResponse>();
    }

    public async Task<IReadOnlyList<WhatsappConversationMessageResponse>> GetWhatsappConversationMessagesAsync(
        string storeId,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<WhatsappConversationMessageResponse>>(
            $"/api/admin/whatsapp/conversations/messages?storeId={Uri.EscapeDataString(storeId)}&phoneNumber={Uri.EscapeDataString(phoneNumber)}",
            cancellationToken) ?? Array.Empty<WhatsappConversationMessageResponse>();
    }

    public async Task<WhatsappContactAgentResponse?> SetWhatsappConversationAgentAsync(
        string storeId,
        string phoneNumber,
        bool isAgentEnabled,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/whatsapp/conversations/agent?phoneNumber={Uri.EscapeDataString(phoneNumber)}")
        {
            Content = JsonContent.Create(new WhatsappContactAgentUpdateRequest(storeId, isAgentEnabled))
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WhatsappContactAgentResponse>(cancellationToken);
    }

    public async Task<WhatsappConversationMessageResponse?> SendWhatsappConversationMessageAsync(
        string storeId,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/admin/whatsapp/conversations/messages?phoneNumber={Uri.EscapeDataString(phoneNumber)}",
            new WhatsappManualMessageRequest(storeId, message),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WhatsappConversationMessageResponse>(cancellationToken);
    }

    public async Task<AgentProductCampaignPreviewResponse?> GetAgentProductCampaignPreviewAsync(
        string storeId,
        string productId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<AgentProductCampaignPreviewResponse>(
            $"/api/admin/agent/product-campaign/preview?storeId={Uri.EscapeDataString(storeId)}&productId={Uri.EscapeDataString(productId)}",
            cancellationToken);
    }

    public async Task<AgentPersonaSettingsResponse> GetAgentPersonaAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<AgentPersonaSettingsResponse>(
            $"/api/admin/agent/persona?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? AgentDefaults.CreateDefaultPersona(storeId);
    }

    public async Task<AgentPersonaSettingsResponse> SaveAgentPersonaAsync(
        AgentPersonaSettingsUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "/api/admin/agent/persona",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentPersonaSettingsResponse>(cancellationToken) ??
            AgentDefaults.CreateDefaultPersona(request.StoreId);
    }

    public async Task<AgentNotificationSettingsResponse> GetAgentNotificationSettingsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<AgentNotificationSettingsResponse>(
            $"/api/admin/agent/notifications/settings?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? AgentDefaults.CreateDefaultNotificationSettings(storeId);
    }

    public async Task<AgentNotificationSettingsResponse> SaveAgentNotificationSettingsAsync(
        AgentNotificationSettingsUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "/api/admin/agent/notifications/settings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentNotificationSettingsResponse>(cancellationToken) ??
            AgentDefaults.CreateDefaultNotificationSettings(request.StoreId);
    }

    public async Task<AgentFeedbackSettingsResponse> GetAgentFeedbackSettingsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<AgentFeedbackSettingsResponse>(
            $"/api/admin/agent/feedback/settings?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? AgentDefaults.CreateDefaultFeedbackSettings(storeId);
    }

    public async Task<AgentFeedbackSettingsResponse> SaveAgentFeedbackSettingsAsync(
        AgentFeedbackSettingsUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "/api/admin/agent/feedback/settings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentFeedbackSettingsResponse>(cancellationToken) ??
            AgentDefaults.CreateDefaultFeedbackSettings(request.StoreId);
    }

    public async Task<IReadOnlyList<AgentFeedbackSolicitationResponse>> GetAgentFeedbackSolicitationsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<AgentFeedbackSolicitationResponse>>(
            $"/api/admin/agent/feedback/solicitations?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<AgentFeedbackSolicitationResponse>();
    }

    public async Task<AgentFeedbackSolicitationResponse?> SendAgentFeedbackSolicitationAsync(
        string storeId,
        string solicitationId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"/api/admin/agent/feedback/solicitations/{Uri.EscapeDataString(solicitationId)}/send?storeId={Uri.EscapeDataString(storeId)}",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentFeedbackSolicitationResponse>(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentAutomatedCampaignResponse>> GetAgentAutomatedCampaignsAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<AgentAutomatedCampaignResponse>>(
            $"/api/admin/agent/automated-campaigns?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<AgentAutomatedCampaignResponse>();
    }

    public async Task<AgentAutomatedCampaignResponse?> CreateAgentAutomatedCampaignAsync(
        AgentAutomatedCampaignUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/agent/automated-campaigns",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentAutomatedCampaignResponse>(cancellationToken);
    }

    public async Task<AgentAutomatedCampaignResponse?> UpdateAgentAutomatedCampaignAsync(
        string campaignId,
        AgentAutomatedCampaignUpsertRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/admin/agent/automated-campaigns/{Uri.EscapeDataString(campaignId)}",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentAutomatedCampaignResponse>(cancellationToken);
    }

    public async Task<AgentAutomatedCampaignRunResponse?> RunAgentAutomatedCampaignAsync(
        string storeId,
        string campaignId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"/api/admin/agent/automated-campaigns/{Uri.EscapeDataString(campaignId)}/run?storeId={Uri.EscapeDataString(storeId)}",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentAutomatedCampaignRunResponse>(cancellationToken);
    }

    public async Task DeleteAgentAutomatedCampaignAsync(
        string storeId,
        string campaignId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.DeleteAsync(
            $"/api/admin/agent/automated-campaigns/{Uri.EscapeDataString(campaignId)}?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<AgentSendResultResponse> SendAgentProductCampaignAsync(
        string storeId,
        string productId,
        string message,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/agent/product-campaign/send",
            new AgentProductCampaignSendRequest(storeId, productId, message),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentSendResultResponse>(cancellationToken) ??
            new AgentSendResultResponse(0, 0, Array.Empty<AgentSendResultItemResponse>());
    }

    public async Task<IReadOnlyList<AgentCustomerRecurrenceResponse>> GetAgentCustomersAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<AgentCustomerRecurrenceResponse>>(
            $"/api/admin/agent/customers?storeId={Uri.EscapeDataString(storeId)}",
            cancellationToken) ?? Array.Empty<AgentCustomerRecurrenceResponse>();
    }

    public async Task<AgentSendResultResponse> SendAgentCustomerReminderAsync(
        string storeId,
        string phoneNumber,
        string message,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/agent/customer-reminder/send",
            new AgentCustomerReminderSendRequest(storeId, phoneNumber, message),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentSendResultResponse>(cancellationToken) ??
            new AgentSendResultResponse(0, 0, Array.Empty<AgentSendResultItemResponse>());
    }

    public async Task<IReadOnlyList<OrderManagementCustomerResponse>> GetManagedOrdersAsync(
        string storeId,
        string? status,
        CancellationToken cancellationToken)
    {
        var path = $"/api/admin/orders/manage?storeId={Uri.EscapeDataString(storeId)}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            path += $"&status={Uri.EscapeDataString(status)}";
        }

        return await _httpClient.GetFromJsonAsync<IReadOnlyList<OrderManagementCustomerResponse>>(
            path,
            cancellationToken) ?? Array.Empty<OrderManagementCustomerResponse>();
    }

    public async Task UpdateOrderStatusAsync(
        string storeId,
        string orderId,
        string status,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/orders/{Uri.EscapeDataString(orderId)}/status")
        {
            Content = JsonContent.Create(new OrderStatusUpdateRequest(storeId, status))
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<OrderHistoryImportResponse> ImportOrderHistoryAsync(
        OrderHistoryImportRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/admin/orders/import-history",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderHistoryImportResponse>(cancellationToken) ??
            new OrderHistoryImportResponse(0, 0, 0, Array.Empty<OrderHistoryImportErrorViewModel>());
    }
}
