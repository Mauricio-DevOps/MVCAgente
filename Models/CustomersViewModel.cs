using System.ComponentModel.DataAnnotations;

namespace Mvc.Models;

public sealed class CustomersViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public CustomerFormModel Form { get; set; } = new();

    public IReadOnlyList<CustomerResponse> Customers { get; set; } = Array.Empty<CustomerResponse>();

    public CustomerImportPreviewModel? ImportPreview { get; set; }

    public IReadOnlyList<CustomerImportErrorViewModel> ImportErrors { get; set; } =
        Array.Empty<CustomerImportErrorViewModel>();

    public CustomerImportSummaryViewModel? ImportSummary { get; set; }
}

public sealed class CustomerFormModel
{
    public string? CustomerId { get; set; }

    public string ClienteNome { get; set; } = string.Empty;

    public string CpfCnpj { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um email valido.")]
    public string ClienteEmail { get; set; } = string.Empty;

    public string ClienteEndereco { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o telefone celular.")]
    public string ClienteTelefoneCelular { get; set; } = string.Empty;

    public bool IsEditing => !string.IsNullOrWhiteSpace(CustomerId);
}

public sealed record CustomerUpsertRequest(
    string StoreId,
    string? ClienteNome,
    string? CpfCnpj,
    string? ClienteEmail,
    string? ClienteEndereco,
    string ClienteTelefoneCelular);

public sealed record CustomerResponse(
    string Id,
    string StoreId,
    string? ClienteNome,
    string? CpfCnpj,
    string? ClienteEmail,
    string? ClienteEndereco,
    string ClienteTelefoneCelular,
    string ClienteDataCriacao);

public static class CustomerImportActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Skip = "Skip";
}

public sealed class CustomerImportPreviewModel
{
    public List<CustomerImportRowInput> Rows { get; set; } = [];

    public bool HasDuplicates => Rows.Any(row => row.IsDuplicate);

    public int ValidRowsCount => Rows.Count;

    public int DuplicateRowsCount => Rows.Count(row => row.IsDuplicate);
}

public sealed class CustomerImportRowInput
{
    public int RowNumber { get; set; }

    public string Action { get; set; } = CustomerImportActions.Create;

    public string? ExistingCustomerId { get; set; }

    public string? ExistingCustomerName { get; set; }

    public string? DuplicateReason { get; set; }

    public string? NewTelefoneCelular { get; set; }

    public string? NewCpfCnpj { get; set; }

    public string ClienteNome { get; set; } = string.Empty;

    public string CpfCnpj { get; set; } = string.Empty;

    public string ClienteEmail { get; set; } = string.Empty;

    public string ClienteEndereco { get; set; } = string.Empty;

    public string ClienteTelefoneCelular { get; set; } = string.Empty;

    public bool IsDuplicate => !string.IsNullOrWhiteSpace(ExistingCustomerId);
}

public sealed record CustomerImportRequest(
    string StoreId,
    IReadOnlyList<CustomerImportRowRequest> Rows);

public sealed record CustomerImportRowRequest(
    int RowNumber,
    string Action,
    string? CustomerId,
    string? ClienteNome,
    string? CpfCnpj,
    string? ClienteEmail,
    string? ClienteEndereco,
    string ClienteTelefoneCelular);

public sealed record CustomerImportResponse(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<CustomerImportErrorViewModel> Errors);

public sealed record CustomerImportErrorViewModel(int RowNumber, string Message);

public sealed record CustomerImportSummaryViewModel(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount);
