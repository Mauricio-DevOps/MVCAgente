using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Models;
using Mvc.Services;

namespace Mvc.Controllers;

[Authorize]
[RequireWhatsAppPortalAccess]
public sealed class CustomersController : Controller
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly string[] TemplateHeaders =
    [
        "CLIENTE_NOME",
        "CPF_CNPJ",
        "CLIENTE_EMAIL",
        "CLIENTE_ENDERECO",
        "CLIENTE_TELEFONE_CELULAR",
        "CLIENTE_DATA_CRIACAO"
    ];

    private readonly ApiClient _apiClient;

    public CustomersController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(
        [FromQuery] string? editCustomerId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(editCustomerId))
        {
            PopulateEditForm(model, editCustomerId);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] CustomerFormModel form,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.Form = form;

        if (!CanUseCustomers(model))
        {
            return View("Index", model);
        }

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var request = new CustomerUpsertRequest(
            model.CompanyPhone,
            NormalizeOptionalText(form.ClienteNome),
            NormalizeOptionalText(form.CpfCnpj),
            NormalizeOptionalText(form.ClienteEmail),
            NormalizeOptionalText(form.ClienteEndereco),
            form.ClienteTelefoneCelular.Trim());

        try
        {
            if (string.IsNullOrWhiteSpace(form.CustomerId))
            {
                await _apiClient.CreateCustomerAsync(request, cancellationToken);
                model.SuccessMessage = "Cliente cadastrado.";
            }
            else
            {
                await _apiClient.UpdateCustomerAsync(form.CustomerId.Trim(), request, cancellationToken);
                model.SuccessMessage = "Cliente atualizado.";
            }

            model.Form = new CustomerFormModel();
            await LoadCustomersAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar o cliente. Verifique se ja existe outro cliente com esse telefone ou CPF/CNPJ.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao salvar o cliente.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        [FromForm] string? customerId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseCustomers(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            model.ErrorMessage = "Cliente invalido.";
            return View("Index", model);
        }

        try
        {
            await _apiClient.DeleteCustomerAsync(
                model.CompanyPhone,
                customerId.Trim(),
                cancellationToken);
            model.SuccessMessage = "Cliente excluido.";
            await LoadCustomersAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel excluir o cliente.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao excluir o cliente.";
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImport(
        IFormFile? customersFile,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseCustomers(model))
        {
            return View("Index", model);
        }

        var parseResult = ParseImportFile(customersFile);
        model.ImportErrors = parseResult.Errors;

        if (parseResult.Rows.Count == 0)
        {
            model.ErrorMessage = parseResult.Errors.Count == 0
                ? "Nenhum cliente valido foi encontrado no arquivo."
                : "Corrija o arquivo e tente importar novamente.";
            return View("Index", model);
        }

        MarkDuplicateRows(parseResult.Rows, model.Customers);
        model.ImportPreview = new CustomerImportPreviewModel { Rows = parseResult.Rows };

        if (model.ImportPreview.HasDuplicates)
        {
            model.ErrorMessage = parseResult.Errors.Count == 0
                ? null
                : "Algumas linhas foram ignoradas por erro. Resolva os duplicados para importar as linhas validas.";
            return View("Index", model);
        }

        await ApplyCustomerImportAsync(model, parseResult.Rows, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(
        [Bind(Prefix = "ImportPreview")] CustomerImportPreviewModel importPreview,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseCustomers(model))
        {
            return View("Index", model);
        }

        var validationErrors = ValidateImportDecisions(importPreview.Rows, model.Customers);
        if (validationErrors.Count > 0)
        {
            model.ImportPreview = importPreview;
            model.ImportErrors = validationErrors;
            model.ErrorMessage = "Revise as decisoes dos clientes duplicados.";
            return View("Index", model);
        }

        await ApplyCustomerImportAsync(model, importPreview.Rows, cancellationToken);
        return View("Index", model);
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Clientes");
        for (var index = 0; index < TemplateHeaders.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = TemplateHeaders[index];
            worksheet.Cell(1, index + 1).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "modelo-clientes.xlsx");
    }

    private async Task ApplyCustomerImportAsync(
        CustomersViewModel model,
        IReadOnlyList<CustomerImportRowInput> rows,
        CancellationToken cancellationToken)
    {
        var requestRows = rows.Select(CreateImportRequestRow).ToArray();

        try
        {
            var response = await _apiClient.ImportCustomersAsync(
                new CustomerImportRequest(model.CompanyPhone, requestRows),
                cancellationToken);

            model.ImportSummary = new CustomerImportSummaryViewModel(
                response.CreatedCount,
                response.UpdatedCount,
                response.SkippedCount);
            model.ImportErrors = response.Errors;
            var changedCount = response.CreatedCount + response.UpdatedCount;
            model.SuccessMessage = changedCount == 0
                ? "Importacao processada sem novos clientes."
                : $"Importacao concluida: {response.CreatedCount} criado(s), {response.UpdatedCount} atualizado(s), {response.SkippedCount} ignorado(s).";
            if (response.Errors.Count > 0)
            {
                model.ErrorMessage = "Algumas linhas nao foram importadas.";
            }

            await LoadCustomersAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel importar os clientes.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao importar os clientes.";
        }
    }

    private static CustomerImportRowRequest CreateImportRequestRow(CustomerImportRowInput row)
    {
        var action = string.IsNullOrWhiteSpace(row.Action)
            ? CustomerImportActions.Create
            : row.Action.Trim();
        var isDuplicateCreate = row.IsDuplicate &&
            string.Equals(action, CustomerImportActions.Create, StringComparison.OrdinalIgnoreCase);
        var phone = isDuplicateCreate && !string.IsNullOrWhiteSpace(row.NewTelefoneCelular)
            ? row.NewTelefoneCelular.Trim()
            : row.ClienteTelefoneCelular.Trim();
        var cpfCnpj = isDuplicateCreate && !string.IsNullOrWhiteSpace(row.NewCpfCnpj)
            ? row.NewCpfCnpj.Trim()
            : NormalizeOptionalText(row.CpfCnpj);

        return new CustomerImportRowRequest(
            row.RowNumber,
            action,
            string.Equals(action, CustomerImportActions.Update, StringComparison.OrdinalIgnoreCase)
                ? row.ExistingCustomerId
                : null,
            NormalizeOptionalText(row.ClienteNome),
            cpfCnpj,
            NormalizeOptionalText(row.ClienteEmail),
            NormalizeOptionalText(row.ClienteEndereco),
            phone);
    }

    private async Task<CustomersViewModel> CreateModelAsync(CancellationToken cancellationToken)
    {
        var model = new CustomersViewModel
        {
            CompanyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa",
            CompanyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return model;
        }

        await LoadCustomersAsync(model, cancellationToken);
        return model;
    }

    private async Task LoadCustomersAsync(CustomersViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.Customers = await _apiClient.GetCustomersAsync(model.CompanyPhone, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar os clientes da API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder aos clientes.";
        }
    }

    private static void PopulateEditForm(CustomersViewModel model, string customerId)
    {
        var customer = model.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, customerId.Trim(), StringComparison.Ordinal));

        if (customer is null)
        {
            model.ErrorMessage = "Cliente nao encontrado para edicao.";
            return;
        }

        model.Form = new CustomerFormModel
        {
            CustomerId = customer.Id,
            ClienteNome = customer.ClienteNome ?? string.Empty,
            CpfCnpj = customer.CpfCnpj ?? string.Empty,
            ClienteEmail = customer.ClienteEmail ?? string.Empty,
            ClienteEndereco = customer.ClienteEndereco ?? string.Empty,
            ClienteTelefoneCelular = customer.ClienteTelefoneCelular
        };
    }

    private static CustomerImportParseResult ParseImportFile(IFormFile? file)
    {
        var rows = new List<CustomerImportRowInput>();
        var errors = new List<CustomerImportErrorViewModel>();

        if (file is null || file.Length == 0)
        {
            errors.Add(new CustomerImportErrorViewModel(0, "Selecione um arquivo .xlsx."));
            return new CustomerImportParseResult(rows, errors);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CustomerImportErrorViewModel(0, "O arquivo deve estar no formato .xlsx."));
            return new CustomerImportParseResult(rows, errors);
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null || worksheet.LastRowUsed() is null)
            {
                errors.Add(new CustomerImportErrorViewModel(0, "A planilha esta vazia."));
                return new CustomerImportParseResult(rows, errors);
            }

            var headers = ReadHeaders(worksheet);
            if (!headers.ContainsKey("clientetelefonecelular"))
            {
                errors.Add(new CustomerImportErrorViewModel(1, "A coluna CLIENTE_TELEFONE_CELULAR e obrigatoria."));
                return new CustomerImportParseResult(rows, errors);
            }

            var seenPhones = new HashSet<string>(StringComparer.Ordinal);
            var seenCpfCnpj = new HashSet<string>(StringComparer.Ordinal);
            var lastRow = worksheet.LastRowUsed()!.RowNumber();
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.CellsUsed().All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
                {
                    continue;
                }

                var name = ReadText(row, headers, "clientenome");
                var cpfCnpj = ReadText(row, headers, "cpfcnpj");
                var email = ReadText(row, headers, "clienteemail");
                var address = ReadText(row, headers, "clienteendereco");
                var phone = ReadText(row, headers, "clientetelefonecelular");

                if (string.IsNullOrWhiteSpace(phone))
                {
                    errors.Add(new CustomerImportErrorViewModel(rowNumber, "Informe o telefone celular do cliente."));
                    continue;
                }

                var normalizedPhone = phone.Trim();
                if (!seenPhones.Add(normalizedPhone))
                {
                    errors.Add(new CustomerImportErrorViewModel(rowNumber, "Telefone duplicado dentro do arquivo."));
                    continue;
                }

                var normalizedCpfCnpj = NormalizeOptionalText(cpfCnpj);
                if (normalizedCpfCnpj is not null && !seenCpfCnpj.Add(normalizedCpfCnpj))
                {
                    errors.Add(new CustomerImportErrorViewModel(rowNumber, "CPF/CNPJ duplicado dentro do arquivo."));
                    continue;
                }

                var normalizedEmail = NormalizeOptionalText(email);
                if (normalizedEmail is not null && !EmailValidator.IsValid(normalizedEmail))
                {
                    errors.Add(new CustomerImportErrorViewModel(rowNumber, "Email invalido."));
                    continue;
                }

                rows.Add(new CustomerImportRowInput
                {
                    RowNumber = rowNumber,
                    Action = CustomerImportActions.Create,
                    ClienteNome = name,
                    CpfCnpj = cpfCnpj,
                    ClienteEmail = email,
                    ClienteEndereco = address,
                    ClienteTelefoneCelular = normalizedPhone
                });
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            errors.Add(new CustomerImportErrorViewModel(0, "Nao foi possivel ler o arquivo .xlsx."));
        }

        return new CustomerImportParseResult(rows, errors);
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var normalized = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(normalized) && !headers.ContainsKey(normalized))
            {
                headers.Add(normalized, cell.Address.ColumnNumber);
            }
        }

        return headers;
    }

    private static string ReadText(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        return headers.TryGetValue(header, out var column)
            ? row.Cell(column).GetString().Trim()
            : string.Empty;
    }

    private static void MarkDuplicateRows(List<CustomerImportRowInput> rows, IReadOnlyList<CustomerResponse> customers)
    {
        var customersByPhone = customers
            .GroupBy(customer => customer.ClienteTelefoneCelular.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var customersByCpfCnpj = customers
            .Where(customer => !string.IsNullOrWhiteSpace(customer.CpfCnpj))
            .GroupBy(customer => customer.CpfCnpj!.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (customersByPhone.TryGetValue(row.ClienteTelefoneCelular.Trim(), out var existingByPhone))
            {
                MarkDuplicate(row, existingByPhone, "telefone");
                continue;
            }

            var cpfCnpj = NormalizeOptionalText(row.CpfCnpj);
            if (cpfCnpj is not null && customersByCpfCnpj.TryGetValue(cpfCnpj, out var existingByCpfCnpj))
            {
                MarkDuplicate(row, existingByCpfCnpj, "CPF/CNPJ");
            }
        }
    }

    private static void MarkDuplicate(CustomerImportRowInput row, CustomerResponse existingCustomer, string reason)
    {
        row.Action = string.Empty;
        row.ExistingCustomerId = existingCustomer.Id;
        row.ExistingCustomerName = GetCustomerDisplayName(existingCustomer);
        row.DuplicateReason = reason;
    }

    private static IReadOnlyList<CustomerImportErrorViewModel> ValidateImportDecisions(
        IReadOnlyList<CustomerImportRowInput> rows,
        IReadOnlyList<CustomerResponse> customers)
    {
        var errors = new List<CustomerImportErrorViewModel>();
        var existingPhones = customers.ToDictionary(
            customer => customer.ClienteTelefoneCelular.Trim(),
            customer => customer.Id,
            StringComparer.Ordinal);
        var existingCpfCnpj = customers
            .Where(customer => !string.IsNullOrWhiteSpace(customer.CpfCnpj))
            .ToDictionary(
                customer => customer.CpfCnpj!.Trim(),
                customer => customer.Id,
                StringComparer.Ordinal);
        var createPhones = new HashSet<string>(StringComparer.Ordinal);
        var createCpfCnpj = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!row.IsDuplicate)
            {
                ValidateCustomerCreateValues(
                    errors,
                    row,
                    row.ClienteTelefoneCelular,
                    NormalizeOptionalText(row.CpfCnpj),
                    existingPhones,
                    existingCpfCnpj,
                    createPhones,
                    createCpfCnpj);
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Action))
            {
                errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "Escolha uma acao para o cliente duplicado."));
                continue;
            }

            if (string.Equals(row.Action, CustomerImportActions.Skip, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Action, CustomerImportActions.Update, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(row.Action, CustomerImportActions.Create, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "Acao invalida para cliente duplicado."));
                continue;
            }

            var newPhone = NormalizeOptionalText(row.NewTelefoneCelular);
            if (newPhone is null)
            {
                errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "Informe um novo telefone para criar outro cliente."));
                continue;
            }

            if (string.Equals(newPhone, row.ClienteTelefoneCelular.Trim(), StringComparison.Ordinal))
            {
                errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "O novo telefone precisa ser diferente do cliente duplicado."));
                continue;
            }

            var originalCpfCnpj = NormalizeOptionalText(row.CpfCnpj);
            var newCpfCnpj = NormalizeOptionalText(row.NewCpfCnpj);
            if (originalCpfCnpj is not null)
            {
                if (newCpfCnpj is null)
                {
                    errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "Informe um novo CPF/CNPJ para criar outro cliente."));
                    continue;
                }

                if (string.Equals(newCpfCnpj, originalCpfCnpj, StringComparison.Ordinal))
                {
                    errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "O novo CPF/CNPJ precisa ser diferente do cliente duplicado."));
                    continue;
                }
            }

            ValidateCustomerCreateValues(
                errors,
                row,
                newPhone,
                newCpfCnpj,
                existingPhones,
                existingCpfCnpj,
                createPhones,
                createCpfCnpj);
        }

        return errors;
    }

    private static void ValidateCustomerCreateValues(
        ICollection<CustomerImportErrorViewModel> errors,
        CustomerImportRowInput row,
        string phone,
        string? cpfCnpj,
        IReadOnlyDictionary<string, string> existingPhones,
        IReadOnlyDictionary<string, string> existingCpfCnpj,
        ISet<string> createPhones,
        ISet<string> createCpfCnpj)
    {
        var normalizedPhone = phone.Trim();
        if (existingPhones.ContainsKey(normalizedPhone) || !createPhones.Add(normalizedPhone))
        {
            errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "O telefone informado ja esta em uso."));
            return;
        }

        if (cpfCnpj is not null &&
            (existingCpfCnpj.ContainsKey(cpfCnpj) || !createCpfCnpj.Add(cpfCnpj)))
        {
            errors.Add(new CustomerImportErrorViewModel(row.RowNumber, "O CPF/CNPJ informado ja esta em uso."));
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeHeader(string? value)
    {
        var normalized = NormalizeForLookup(value);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string NormalizeForLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhiteSpace = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhiteSpace)
                {
                    builder.Append(' ');
                    previousWasWhiteSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhiteSpace = false;
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static string GetCustomerDisplayName(CustomerResponse customer)
    {
        return string.IsNullOrWhiteSpace(customer.ClienteNome)
            ? customer.ClienteTelefoneCelular
            : $"{customer.ClienteNome} ({customer.ClienteTelefoneCelular})";
    }

    private static bool CanUseCustomers(CustomersViewModel model)
    {
        return !string.IsNullOrWhiteSpace(model.CompanyPhone);
    }

    private sealed record CustomerImportParseResult(
        List<CustomerImportRowInput> Rows,
        IReadOnlyList<CustomerImportErrorViewModel> Errors);
}
