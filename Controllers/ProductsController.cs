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
public sealed class ProductsController : Controller
{
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] TemplateHeaders =
    [
        "id",
        "tipo",
        "ativo",
        "nome",
        "descricao-completa",
        "preco-cheio",
        "preco-promocional",
        "marca"
    ];

    private readonly ApiClient _apiClient;
    private readonly RestaurantMenuApiClient _restaurantMenuApiClient;

    public ProductsController(ApiClient apiClient, RestaurantMenuApiClient restaurantMenuApiClient)
    {
        _apiClient = apiClient;
        _restaurantMenuApiClient = restaurantMenuApiClient;
    }

    public async Task<IActionResult> Index(
        [FromQuery] string? editProductId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(editProductId))
        {
            PopulateEditForm(model, editProductId);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] ProductFormModel form,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        model.Form = form;

        if (!CanUseProducts(model))
        {
            return View("Index", model);
        }

        if (form.PromotionalPrice > form.RetailPrice)
        {
            ModelState.AddModelError("Form.PromotionalPrice", "O preco promocional nao pode ser maior que o preco cheio.");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var request = new ProductUpsertRequest(
            model.CompanyPhone,
            form.Name.Trim(),
            NormalizeOptionalText(form.Description),
            NormalizeOptionalText(form.Type),
            NormalizeOptionalText(form.Brand),
            form.RetailPrice,
            form.PromotionalPrice,
            form.WholesalePrice,
            ParseAliases(form.AliasesText),
            form.StockQuantity,
            form.LowStockThreshold,
            form.IsActive);

        ProductResponse? savedProduct;
        try
        {
            if (string.IsNullOrWhiteSpace(form.ProductId))
            {
                savedProduct = await _apiClient.CreateProductAsync(request, cancellationToken);
                model.SuccessMessage = "Produto cadastrado.";
            }
            else
            {
                savedProduct = await _apiClient.UpdateProductAsync(form.ProductId.Trim(), request, cancellationToken);
                model.SuccessMessage = "Produto atualizado.";
            }
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel salvar o produto. Verifique se ja existe outro produto com esse nome.";
            return View("Index", model);
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao salvar o produto.";
            return View("Index", model);
        }

        if (form.SyncToMenu && savedProduct is not null)
        {
            try
            {
                await _restaurantMenuApiClient.SyncProductToMenuAsync(
                    new ProductMenuSyncRequest(
                        model.CompanyPhone,
                        savedProduct.Id,
                        savedProduct.Name,
                        savedProduct.Description,
                        savedProduct.RetailPrice),
                    cancellationToken);
                model.SuccessMessage += " Enviado ao cardapio como rascunho.";
            }
            catch (HttpRequestException)
            {
                model.ErrorMessage = "Produto salvo, mas nao foi possivel enviar ao cardapio.";
            }
            catch (TaskCanceledException)
            {
                model.ErrorMessage = "Produto salvo, mas o cardapio demorou para responder.";
            }
        }

        model.Form = new ProductFormModel();
        await LoadProductsAsync(model, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImport(
        IFormFile? productsFile,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseProducts(model))
        {
            return View("Index", model);
        }

        var parseResult = ParseImportFile(productsFile);
        model.ImportErrors = parseResult.Errors;

        if (parseResult.Rows.Count == 0)
        {
            model.ErrorMessage = parseResult.Errors.Count == 0
                ? "Nenhum produto valido foi encontrado no arquivo."
                : "Corrija o arquivo e tente importar novamente.";
            return View("Index", model);
        }

        MarkDuplicateRows(parseResult.Rows, model.Products);
        model.ImportPreview = new ProductImportPreviewModel { Rows = parseResult.Rows };

        if (model.ImportPreview.HasDuplicates)
        {
            model.ErrorMessage = parseResult.Errors.Count == 0
                ? null
                : "Algumas linhas foram ignoradas por erro. Resolva os duplicados para importar as linhas validas.";
            return View("Index", model);
        }

        await ApplyProductImportAsync(model, parseResult.Rows, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(
        [Bind(Prefix = "ImportPreview")] ProductImportPreviewModel importPreview,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseProducts(model))
        {
            return View("Index", model);
        }

        var validationErrors = ValidateImportDecisions(importPreview.Rows, model.Products);
        if (validationErrors.Count > 0)
        {
            model.ImportPreview = importPreview;
            model.ImportErrors = validationErrors;
            model.ErrorMessage = "Revise as decisoes dos produtos duplicados.";
            return View("Index", model);
        }

        await ApplyProductImportAsync(model, importPreview.Rows, cancellationToken);
        return View("Index", model);
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Produtos");
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
            "modelo-produtos.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivate(
        [FromForm] string? productId,
        CancellationToken cancellationToken)
    {
        var model = await CreateModelAsync(cancellationToken);
        if (!CanUseProducts(model))
        {
            return View("Index", model);
        }

        if (string.IsNullOrWhiteSpace(productId))
        {
            model.ErrorMessage = "Produto invalido.";
            return View("Index", model);
        }

        try
        {
            await _apiClient.InactivateProductAsync(
                model.CompanyPhone,
                productId.Trim(),
                cancellationToken);
            model.SuccessMessage = "Produto inativado.";
            await LoadProductsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel inativar o produto.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao inativar o produto.";
        }

        return View("Index", model);
    }

    private async Task ApplyProductImportAsync(
        ProductsViewModel model,
        IReadOnlyList<ProductImportRowInput> rows,
        CancellationToken cancellationToken)
    {
        var requestRows = rows.Select(CreateImportRequestRow).ToArray();

        try
        {
            var response = await _apiClient.ImportProductsAsync(
                new ProductImportRequest(model.CompanyPhone, requestRows),
                cancellationToken);

            model.ImportSummary = new ProductImportSummaryViewModel(
                response.CreatedCount,
                response.UpdatedCount,
                response.SkippedCount);
            model.ImportErrors = response.Errors;
            var changedCount = response.CreatedCount + response.UpdatedCount;
            model.SuccessMessage = changedCount == 0
                ? "Importacao processada sem novos produtos."
                : $"Importacao concluida: {response.CreatedCount} criado(s), {response.UpdatedCount} atualizado(s), {response.SkippedCount} ignorado(s).";
            if (response.Errors.Count > 0)
            {
                model.ErrorMessage = "Algumas linhas nao foram importadas.";
            }

            await LoadProductsAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel importar os produtos.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder ao importar os produtos.";
        }
    }

    private static ProductImportRowRequest CreateImportRequestRow(ProductImportRowInput row)
    {
        var action = string.IsNullOrWhiteSpace(row.Action)
            ? ProductImportActions.Create
            : row.Action.Trim();

        var name = string.Equals(action, ProductImportActions.Create, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(row.NewName)
                ? row.NewName.Trim()
                : row.Name.Trim();

        return new ProductImportRowRequest(
            row.RowNumber,
            action,
            string.Equals(action, ProductImportActions.Update, StringComparison.OrdinalIgnoreCase)
                ? row.ExistingProductId
                : null,
            name,
            NormalizeOptionalText(row.Description),
            NormalizeOptionalText(row.Type),
            NormalizeOptionalText(row.Brand),
            row.RetailPrice,
            row.PromotionalPrice,
            row.IsActive);
    }

    private async Task<ProductsViewModel> CreateModelAsync(CancellationToken cancellationToken)
    {
        var model = new ProductsViewModel
        {
            CompanyName = User.FindFirst(AuthClaims.CompanyName)?.Value ?? "Empresa",
            CompanyPhone = User.FindFirst(AuthClaims.CompanyPhone)?.Value ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(model.CompanyPhone))
        {
            model.ErrorMessage = "Login sem telefone de empresa vinculado.";
            return model;
        }

        await LoadProductsAsync(model, cancellationToken);
        return model;
    }

    private async Task LoadProductsAsync(ProductsViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.Products = await _apiClient.GetProductsAsync(model.CompanyPhone, cancellationToken);
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "Nao foi possivel carregar os produtos da API.";
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "A API demorou para responder aos produtos.";
        }
    }

    private static void PopulateEditForm(ProductsViewModel model, string productId)
    {
        var product = model.Products.FirstOrDefault(item =>
            string.Equals(item.Id, productId.Trim(), StringComparison.Ordinal));

        if (product is null)
        {
            model.ErrorMessage = "Produto nao encontrado para edicao.";
            return;
        }

        model.Form = new ProductFormModel
        {
            ProductId = product.Id,
            Name = product.Name,
            Description = product.Description ?? string.Empty,
            Type = product.Type ?? string.Empty,
            Brand = product.Brand ?? string.Empty,
            RetailPrice = product.RetailPrice,
            PromotionalPrice = product.PromotionalPrice,
            WholesalePrice = product.WholesalePrice,
            StockQuantity = product.StockQuantity,
            LowStockThreshold = product.LowStockThreshold,
            AliasesText = string.Join(Environment.NewLine, product.Aliases),
            IsActive = product.IsActive
        };
    }

    private static ProductImportParseResult ParseImportFile(IFormFile? file)
    {
        var rows = new List<ProductImportRowInput>();
        var errors = new List<ProductImportErrorViewModel>();

        if (file is null || file.Length == 0)
        {
            errors.Add(new ProductImportErrorViewModel(0, "Selecione um arquivo .xlsx."));
            return new ProductImportParseResult(rows, errors);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ProductImportErrorViewModel(0, "O arquivo deve estar no formato .xlsx."));
            return new ProductImportParseResult(rows, errors);
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null || worksheet.LastRowUsed() is null)
            {
                errors.Add(new ProductImportErrorViewModel(0, "A planilha esta vazia."));
                return new ProductImportParseResult(rows, errors);
            }

            var headers = ReadHeaders(worksheet);
            if (!headers.ContainsKey("nome"))
            {
                errors.Add(new ProductImportErrorViewModel(1, "A coluna nome e obrigatoria."));
            }

            if (!headers.ContainsKey("precocheio"))
            {
                errors.Add(new ProductImportErrorViewModel(1, "A coluna preco-cheio e obrigatoria."));
            }

            if (errors.Count > 0)
            {
                return new ProductImportParseResult(rows, errors);
            }

            var lastRow = worksheet.LastRowUsed()!.RowNumber();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.CellsUsed().All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
                {
                    continue;
                }

                var name = ReadText(row, headers, "nome");
                var description = ReadText(row, headers, "descricaocompleta");
                var type = ReadText(row, headers, "tipo");
                var brand = ReadText(row, headers, "marca");
                var activeText = ReadText(row, headers, "ativo");

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "Informe o nome do produto."));
                    continue;
                }

                var normalizedName = NormalizeForLookup(name);
                if (!seenNames.Add(normalizedName))
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "Nome duplicado dentro do arquivo."));
                    continue;
                }

                if (!TryReadDecimal(row, headers, "precocheio", required: true, out var retailPrice, out var priceError))
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, priceError ?? "Preco cheio invalido."));
                    continue;
                }

                if (retailPrice < 0)
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "O preco cheio nao pode ser negativo."));
                    continue;
                }

                if (!TryReadDecimal(row, headers, "precopromocional", required: false, out var promotionalPrice, out var promotionalPriceError))
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, promotionalPriceError ?? "Preco promocional invalido."));
                    continue;
                }

                if (promotionalPrice < 0)
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "O preco promocional nao pode ser negativo."));
                    continue;
                }

                if (promotionalPrice > retailPrice)
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "O preco promocional nao pode ser maior que o preco cheio."));
                    continue;
                }

                if (!TryParseActive(activeText, out var isActive))
                {
                    errors.Add(new ProductImportErrorViewModel(rowNumber, "Valor invalido na coluna ativo."));
                    continue;
                }

                rows.Add(new ProductImportRowInput
                {
                    RowNumber = rowNumber,
                    Action = ProductImportActions.Create,
                    Name = name.Trim(),
                    Description = description,
                    Type = type,
                    Brand = brand,
                    RetailPrice = retailPrice,
                    PromotionalPrice = promotionalPrice,
                    IsActive = isActive
                });
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            errors.Add(new ProductImportErrorViewModel(0, "Nao foi possivel ler o arquivo .xlsx."));
        }

        return new ProductImportParseResult(rows, errors);
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

    private static bool TryReadDecimal(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string header,
        bool required,
        out decimal value,
        out string? error)
    {
        value = 0;
        error = null;
        if (!headers.TryGetValue(header, out var column))
        {
            return !required;
        }

        var cell = row.Cell(column);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))
        {
            if (!required)
            {
                return true;
            }

            error = "Informe o preco cheio.";
            return false;
        }

        if (cell.TryGetValue<double>(out var numericValue))
        {
            value = Convert.ToDecimal(numericValue, CultureInfo.InvariantCulture);
            return true;
        }

        var text = cell.GetString().Trim();
        foreach (var culture in new[] { PtBrCulture, CultureInfo.InvariantCulture, CultureInfo.CurrentCulture })
        {
            if (decimal.TryParse(text, NumberStyles.Currency, culture, out value))
            {
                return true;
            }
        }

        error = header == "precocheio" ? "Preco cheio invalido." : "Preco promocional invalido.";
        return false;
    }

    private static bool TryParseActive(string? value, out bool isActive)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            isActive = true;
            return true;
        }

        var normalized = NormalizeHeader(value);
        switch (normalized)
        {
            case "true":
            case "sim":
            case "s":
            case "1":
            case "ativo":
            case "ativa":
                isActive = true;
                return true;
            case "false":
            case "nao":
            case "n":
            case "0":
            case "inativo":
            case "inativa":
                isActive = false;
                return true;
            default:
                isActive = true;
                return false;
        }
    }

    private static void MarkDuplicateRows(List<ProductImportRowInput> rows, IReadOnlyList<ProductResponse> products)
    {
        var productsByName = products
            .GroupBy(product => NormalizeForLookup(product.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!productsByName.TryGetValue(NormalizeForLookup(row.Name), out var existingProduct))
            {
                continue;
            }

            row.Action = string.Empty;
            row.ExistingProductId = existingProduct.Id;
            row.ExistingProductName = existingProduct.Name;
        }
    }

    private static IReadOnlyList<ProductImportErrorViewModel> ValidateImportDecisions(
        IReadOnlyList<ProductImportRowInput> rows,
        IReadOnlyList<ProductResponse> products)
    {
        var errors = new List<ProductImportErrorViewModel>();
        var existingNames = products
            .ToDictionary(product => NormalizeForLookup(product.Name), product => product.Id, StringComparer.Ordinal);
        var createNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!row.IsDuplicate)
            {
                var normalizedName = NormalizeForLookup(row.Name);
                if (!createNames.Add(normalizedName))
                {
                    errors.Add(new ProductImportErrorViewModel(row.RowNumber, "Nome duplicado entre produtos a criar."));
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Action))
            {
                errors.Add(new ProductImportErrorViewModel(row.RowNumber, "Escolha uma acao para o produto duplicado."));
                continue;
            }

            if (string.Equals(row.Action, ProductImportActions.Skip, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Action, ProductImportActions.Update, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(row.Action, ProductImportActions.Create, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ProductImportErrorViewModel(row.RowNumber, "Acao invalida para produto duplicado."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.NewName))
            {
                errors.Add(new ProductImportErrorViewModel(row.RowNumber, "Informe um novo nome para criar outro produto."));
                continue;
            }

            var normalizedNewName = NormalizeForLookup(row.NewName);
            if (string.Equals(normalizedNewName, NormalizeForLookup(row.Name), StringComparison.Ordinal))
            {
                errors.Add(new ProductImportErrorViewModel(row.RowNumber, "O novo nome precisa ser diferente do produto duplicado."));
                continue;
            }

            if (existingNames.ContainsKey(normalizedNewName) || !createNames.Add(normalizedNewName))
            {
                errors.Add(new ProductImportErrorViewModel(row.RowNumber, "O novo nome ja esta em uso."));
            }
        }

        return errors;
    }

    private static IReadOnlyList<string> ParseAliases(string? aliasesText)
    {
        if (string.IsNullOrWhiteSpace(aliasesText))
        {
            return Array.Empty<string>();
        }

        return aliasesText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static bool CanUseProducts(ProductsViewModel model)
    {
        return !string.IsNullOrWhiteSpace(model.CompanyPhone);
    }

    private sealed record ProductImportParseResult(
        List<ProductImportRowInput> Rows,
        IReadOnlyList<ProductImportErrorViewModel> Errors);
}
