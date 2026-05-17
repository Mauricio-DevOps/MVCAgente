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
public sealed class OrdersController : Controller
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] HistoryTemplateHeaders =
    [
        "PEDIDO_CODIGO",
        "PEDIDO_DATA",
        "CLIENTE_NOME",
        "CPF_CNPJ",
        "CLIENTE_EMAIL",
        "CLIENTE_TELEFONE_CELULAR",
        "TIPO_VENDA",
        "PRODUTO_NOME",
        "QUANTIDADE",
        "PRECO_UNITARIO",
        "OBSERVACAO_ITEM",
        "OBSERVACAO_PEDIDO"
    ];

    private static readonly string[] ValidStatuses =
    {
        "PendingReview",
        "EmProducao",
        "EmRotaEntrega",
        "Concluido"
    };

    private readonly ApiClient _apiClient;

    public OrdersController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(status, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        [FromForm] string? orderId,
        [FromForm] string? newStatus,
        [FromForm] string? selectedStatus,
        CancellationToken cancellationToken)
    {
        var model = CreateBaseModel(selectedStatus);
        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(orderId) || !IsValidStatus(newStatus))
        {
            model.ErrorMessage = "Pedido ou status inválido.";
            await LoadOrdersAsync(model, cancellationToken);
            return View("Index", model);
        }

        try
        {
            await _apiClient.UpdateOrderStatusAsync(
                model.CompanyPhone,
                orderId.Trim(),
                newStatus!.Trim(),
                cancellationToken);
            model.SuccessMessage = "Status do pedido atualizado.";
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Não foi possível atualizar o status do pedido.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder à atualização do status.";
        }

        await LoadOrdersAsync(model, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadHistoryImport(
        IFormFile? ordersFile,
        [FromForm] string? selectedStatus,
        CancellationToken cancellationToken)
    {
        var model = CreateBaseModel(selectedStatus);
        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return View("Index", model);
        }

        var parseResult = ParseHistoryImportFile(ordersFile);
        model.ImportErrors = parseResult.Errors;

        if (parseResult.Rows.Count == 0)
        {
            model.ErrorMessage = parseResult.Errors.Count == 0
                ? "Nenhum pedido valido foi encontrado no arquivo."
                : "Corrija o arquivo e tente importar novamente.";
            await LoadOrdersAsync(model, cancellationToken);
            return View("Index", model);
        }

        await ApplyHistoryImportAsync(model, parseResult.Rows, cancellationToken);
        return View("Index", model);
    }

    [HttpGet]
    public IActionResult DownloadHistoryTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("HistoricoPedidos");
        for (var index = 0; index < HistoryTemplateHeaders.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = HistoryTemplateHeaders[index];
            worksheet.Cell(1, index + 1).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "modelo-historico-pedidos.xlsx");
    }

    private async Task ApplyHistoryImportAsync(
        OrdersViewModel model,
        IReadOnlyList<OrderHistoryImportRowRequest> rows,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.ImportOrderHistoryAsync(
                new OrderHistoryImportRequest(model.CompanyPhone, rows),
                cancellationToken);

            model.ImportSummary = new OrderHistoryImportSummaryViewModel(
                response.CreatedOrderCount,
                response.CreatedItemCount,
                response.SkippedOrderCount);
            model.ImportErrors = model.ImportErrors.Concat(response.Errors).ToArray();
            model.SuccessMessage = response.CreatedOrderCount == 0
                ? "Importacao processada sem novos pedidos."
                : $"Importacao concluida: {response.CreatedOrderCount} pedido(s), {response.CreatedItemCount} item(ns), {response.SkippedOrderCount} duplicado(s) ignorado(s).";
            if (model.ImportErrors.Count > 0)
            {
                model.ErrorMessage = "Algumas linhas nao foram importadas.";
            }

            await LoadOrdersAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel importar o historico de pedidos.";
            await LoadOrdersAsync(model, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao importar o historico de pedidos.";
            await LoadOrdersAsync(model, cancellationToken);
        }
    }

    private async Task<OrdersViewModel> CreateModelAsync(
        string? selectedStatus,
        CancellationToken cancellationToken)
    {
        var model = CreateBaseModel(selectedStatus);

        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return model;
        }

        if (!string.IsNullOrWhiteSpace(selectedStatus) && model.SelectedStatus is null)
        {
            model.ErrorMessage = "Status inválido.";
            return model;
        }

        await LoadOrdersAsync(model, cancellationToken);
        return model;
    }

    private OrdersViewModel CreateBaseModel(string? selectedStatus)
    {
        return new OrdersViewModel
        {
            CompanyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa",
            CompanyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty,
            SelectedStatus = NormalizeStatus(selectedStatus)
        };
    }

    private async Task LoadOrdersAsync(OrdersViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.Customers = await _apiClient.GetManagedOrdersAsync(
                model.CompanyPhone,
                model.SelectedStatus,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Não foi possível carregar os pedidos da API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder aos pedidos.";
        }
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim();
        return IsValidStatus(trimmed) ? trimmed : null;
    }

    private static bool IsValidStatus(string? status)
    {
        return ValidStatuses.Contains(status, StringComparer.Ordinal);
    }

    private static OrderHistoryImportParseResult ParseHistoryImportFile(IFormFile? file)
    {
        var rows = new List<OrderHistoryImportRowRequest>();
        var errors = new List<OrderHistoryImportErrorViewModel>();

        if (file is null || file.Length == 0)
        {
            errors.Add(new OrderHistoryImportErrorViewModel(0, "Selecione um arquivo .xlsx."));
            return new OrderHistoryImportParseResult(rows, errors);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new OrderHistoryImportErrorViewModel(0, "O arquivo deve estar no formato .xlsx."));
            return new OrderHistoryImportParseResult(rows, errors);
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null || worksheet.LastRowUsed() is null)
            {
                errors.Add(new OrderHistoryImportErrorViewModel(0, "A planilha esta vazia."));
                return new OrderHistoryImportParseResult(rows, errors);
            }

            var headers = ReadHeaders(worksheet);
            foreach (var requiredHeader in new[] { "pedidocodigo", "clientetelefonecelular", "produtonome", "quantidade" })
            {
                if (!headers.ContainsKey(requiredHeader))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(1, $"A coluna {HeaderLabel(requiredHeader)} e obrigatoria."));
                }
            }

            if (errors.Count > 0)
            {
                return new OrderHistoryImportParseResult(rows, errors);
            }

            var lastRow = worksheet.LastRowUsed()!.RowNumber();
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.CellsUsed().All(cell => cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString())))
                {
                    continue;
                }

                var pedidoCodigo = ReadText(row, headers, "pedidocodigo");
                var clienteTelefoneCelular = ReadText(row, headers, "clientetelefonecelular");
                var produtoNome = ReadText(row, headers, "produtonome");
                var clienteEmail = ReadText(row, headers, "clienteemail");

                if (string.IsNullOrWhiteSpace(pedidoCodigo))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "Informe o PEDIDO_CODIGO."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clienteTelefoneCelular))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "Informe o telefone celular do cliente."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(produtoNome))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "Informe o nome do produto."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(clienteEmail) && !EmailValidator.IsValid(clienteEmail.Trim()))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "Email invalido."));
                    continue;
                }

                if (!TryReadInt(row, headers, "quantidade", out var quantidade, out var quantityError))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, quantityError ?? "Quantidade invalida."));
                    continue;
                }

                if (quantidade <= 0)
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "A quantidade deve ser maior que zero."));
                    continue;
                }

                if (!TryReadDecimal(row, headers, "precounitario", out var precoUnitario, out var priceError))
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, priceError ?? "Preco unitario invalido."));
                    continue;
                }

                if (precoUnitario is < 0)
                {
                    errors.Add(new OrderHistoryImportErrorViewModel(rowNumber, "O preco unitario nao pode ser negativo."));
                    continue;
                }

                rows.Add(new OrderHistoryImportRowRequest(
                    rowNumber,
                    pedidoCodigo.Trim(),
                    ReadDateText(row, headers, "pedidodata"),
                    ReadText(row, headers, "clientenome"),
                    ReadText(row, headers, "cpfcnpj"),
                    clienteEmail.Trim(),
                    clienteTelefoneCelular.Trim(),
                    ReadText(row, headers, "tipovenda"),
                    produtoNome.Trim(),
                    quantidade,
                    precoUnitario,
                    ReadText(row, headers, "observacaoitem"),
                    ReadText(row, headers, "observacaopedido")));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            errors.Add(new OrderHistoryImportErrorViewModel(0, "Nao foi possivel ler o arquivo .xlsx."));
        }

        return new OrderHistoryImportParseResult(rows, errors);
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

    private static string? ReadDateText(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        if (!headers.TryGetValue(header, out var column))
        {
            return null;
        }

        var cell = row.Cell(column);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dateValue))
        {
            return dateValue.ToString("O", CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }

    private static bool TryReadInt(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string header,
        out int value,
        out string? error)
    {
        value = 0;
        error = null;
        var cell = row.Cell(headers[header]);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            error = "Informe a quantidade.";
            return false;
        }

        if (cell.TryGetValue<int>(out value))
        {
            return true;
        }

        if (int.TryParse(cell.GetString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = "Quantidade invalida.";
        return false;
    }

    private static bool TryReadDecimal(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string header,
        out decimal? value,
        out string? error)
    {
        value = null;
        error = null;
        if (!headers.TryGetValue(header, out var column))
        {
            return true;
        }

        var cell = row.Cell(column);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            return true;
        }

        if (cell.TryGetValue<double>(out var numericValue))
        {
            value = Convert.ToDecimal(numericValue, CultureInfo.InvariantCulture);
            return true;
        }

        var text = cell.GetString().Trim();
        foreach (var culture in new[] { PtBrCulture, CultureInfo.InvariantCulture, CultureInfo.CurrentCulture })
        {
            if (decimal.TryParse(text, NumberStyles.Currency, culture, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        error = "Preco unitario invalido.";
        return false;
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

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static string HeaderLabel(string normalizedHeader)
    {
        return normalizedHeader switch
        {
            "pedidocodigo" => "PEDIDO_CODIGO",
            "clientetelefonecelular" => "CLIENTE_TELEFONE_CELULAR",
            "produtonome" => "PRODUTO_NOME",
            "quantidade" => "QUANTIDADE",
            _ => normalizedHeader
        };
    }

    private sealed record OrderHistoryImportParseResult(
        List<OrderHistoryImportRowRequest> Rows,
        IReadOnlyList<OrderHistoryImportErrorViewModel> Errors);
}
