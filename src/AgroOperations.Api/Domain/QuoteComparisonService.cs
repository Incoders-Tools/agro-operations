namespace AgroOperations.Api.Domain;

public sealed record QuoteComparison(Guid Id, Guid ClientId, string Title, DateOnly RequestedOn, string Status, Guid? ChosenQuoteId, string? DecisionReason,
    long Revision);

/// <summary>A line names a catalog input, a free product text, or both.</summary>
public sealed record QuoteLine(Guid Id, short Position, Guid? InputId, string? ProductText, decimal Quantity, string Unit, decimal UnitPrice)
{
    // Rounded half away from zero, as PostgreSQL rounds numeric values.
    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 4, MidpointRounding.AwayFromZero);
}
public sealed record Quote(Guid Id, Guid ComparisonId, Guid ProviderId, DateOnly ReceivedOn, string Currency, string? Notes, IReadOnlyList<QuoteLine> Lines,
    long Revision)
{
    public decimal Total => decimal.Round(Lines.Sum(line => line.Quantity * line.UnitPrice), 4, MidpointRounding.AwayFromZero);
}

/// <summary>An absent unit is the input's unit; a line without an input then needs one.</summary>
public sealed record QuoteLineDraft(Guid? InputId, string? ProductText, decimal Quantity, string? Unit, decimal UnitPrice);
/// <summary>Writes replace the quote and its lines. Create requires the provider; on update an absent one keeps the current one.</summary>
public sealed record QuoteDraft(Guid? ProviderId, DateOnly? ReceivedOn, string? Currency, string? Notes, IReadOnlyList<QuoteLineDraft>? Lines);

public sealed record QuoteOffer(Guid QuoteId, Guid ProviderId, decimal UnitPrice, string Currency, decimal Quantity, string Unit, decimal LineTotal);
/// <summary>Key is "input:&lt;inputId&gt;" or "text:&lt;lowercased trimmed product text&gt;".</summary>
public sealed record QuoteProduct(string Key, string Label, IReadOnlyList<QuoteOffer> Offers, IReadOnlyDictionary<string, Guid> CheapestQuoteIdByCurrency);
public sealed record QuoteComparisonDetail(QuoteComparison Comparison, IReadOnlyList<Quote> Quotes, IReadOnlyList<QuoteProduct> Products);

public interface IQuoteComparisonRepository
{
    QuoteComparison Create(Guid clientId, string title, DateOnly requestedOn);
    /// <summary>Only an abierta comparison changes; a decidida one is not open.</summary>
    QuoteComparison Update(Guid id, long revision, string title, DateOnly requestedOn);
    /// <summary>A quote of another comparison is not found.</summary>
    QuoteComparison Decide(Guid id, long revision, Guid quoteId, string reason);
    QuoteComparison Reopen(Guid id, long revision);
    IReadOnlyList<QuoteComparison> List(Guid? clientId, string? status);
    /// <summary>The comparison, its quotes ordered by received date then creation, and the current names of the inputs they quote.</summary>
    (QuoteComparison Comparison, IReadOnlyList<Quote> Quotes, IReadOnlyDictionary<Guid, string> InputNames)? Detail(Guid id);
    Quote CreateQuote(Guid comparisonId, QuoteDraft quote);
    Quote UpdateQuote(Guid id, long revision, QuoteDraft quote);
}

/// <summary>
/// A client's comparison of the quotes of several providers. Deciding records the chosen quote and the reason; reopening
/// keeps both until the next decision. Quotes and the comparison change only while it is abierta.
/// </summary>
public sealed class QuoteComparisonService(IQuoteComparisonRepository repository)
{
    private static readonly string[] Statuses = ["abierta", "decidida"];

    private static (string Title, DateOnly RequestedOn) Validated(string? title, DateOnly? requestedOn) =>
        (CatalogText.Required(title, "Title"), requestedOn ?? throw new DomainValidationException("Requested on is required"));

    private static QuoteDraft Validated(QuoteDraft draft)
    {
        if (draft.ReceivedOn is null) throw new DomainValidationException("Received on is required");
        var currency = CatalogText.Currency(draft.Currency);
        var lines = (draft.Lines ?? []).Select(line =>
        {
            var product = CatalogText.Optional(line.ProductText);
            if (line.InputId is null && product is null) throw new DomainValidationException("Each line needs an input or a product");
            if (line.Quantity <= 0 || !CatalogText.FitsAmount(line.Quantity)) throw new DomainValidationException("Quantity must be positive");
            if (line.UnitPrice < 0 || !CatalogText.FitsAmount(line.UnitPrice)) throw new DomainValidationException("Unit price must be zero or positive");
            var unit = CatalogText.Optional(line.Unit);
            if (unit is null && line.InputId is null) throw new DomainValidationException("Unit is required");
            return line with { ProductText = product, Unit = unit };
        }).ToArray();
        return draft with { Currency = currency, Notes = CatalogText.Optional(draft.Notes), Lines = lines };
    }

    public QuoteComparison Create(Guid clientId, string? title, DateOnly? requestedOn)
    {
        var (validTitle, date) = Validated(title, requestedOn);
        return repository.Create(clientId, validTitle, date);
    }

    public QuoteComparison Update(Guid id, long revision, string? title, DateOnly? requestedOn)
    {
        var (validTitle, date) = Validated(title, requestedOn);
        return repository.Update(id, revision, validTitle, date);
    }

    public QuoteComparison Decide(Guid id, long revision, Guid quoteId, string? reason) =>
        repository.Decide(id, revision, quoteId, CatalogText.Optional(reason) ?? throw new DomainValidationException("Reason is required"));
    public QuoteComparison Reopen(Guid id, long revision) => repository.Reopen(id, revision);

    /// <summary>Newest request first; an unknown status is rejected rather than matching nothing.</summary>
    public IReadOnlyList<QuoteComparison> List(Guid? clientId = null, string? status = null)
    {
        var filter = CatalogText.Optional(status)?.ToLowerInvariant();
        if (filter is not null && Array.IndexOf(Statuses, filter) < 0)
            throw new DomainValidationException($"Status must be one of: {string.Join(", ", Statuses)}");
        return repository.List(clientId, filter);
    }

    public Quote AddQuote(Guid comparisonId, QuoteDraft quote)
    {
        var validated = Validated(quote);
        if (validated.ProviderId is null) throw new DomainValidationException("Provider is required");
        return repository.CreateQuote(comparisonId, validated);
    }

    public Quote UpdateQuote(Guid id, long revision, QuoteDraft quote) => repository.UpdateQuote(id, revision, Validated(quote));

    public QuoteComparisonDetail? Detail(Guid id) =>
        repository.Detail(id) is { } found ? new(found.Comparison, found.Quotes, Products(found.Quotes, found.InputNames)) : null;

    /// <summary>
    /// Every quoted line grouped by product, ordered by label. In each currency the cheapest quote has the lowest unit price;
    /// a tie goes to the quote received first, then to the earlier quote in the list, then to the lower line position.
    /// </summary>
    private static IReadOnlyList<QuoteProduct> Products(IReadOnlyList<Quote> quotes, IReadOnlyDictionary<Guid, string> inputNames)
    {
        var offers = quotes.SelectMany((quote, order) => quote.Lines.Select(line => (Quote: quote, Order: order, Line: line,
            Key: line.InputId is { } inputId ? $"input:{inputId}" : $"text:{line.ProductText!.ToLowerInvariant()}")));
        return offers.GroupBy(offer => offer.Key).Select(group =>
        {
            var first = group.First();
            var label = first.Line.InputId is { } inputId ? inputNames[inputId] : first.Line.ProductText!;
            var cheapest = group.GroupBy(offer => offer.Quote.Currency).OrderBy(currency => currency.Key, StringComparer.Ordinal).ToDictionary(currency => currency.Key,
                currency => currency.OrderBy(offer => offer.Line.UnitPrice).ThenBy(offer => offer.Quote.ReceivedOn).ThenBy(offer => offer.Order)
                    .ThenBy(offer => offer.Line.Position).First().Quote.Id);
            return new QuoteProduct(group.Key, label, group.Select(offer => new QuoteOffer(offer.Quote.Id, offer.Quote.ProviderId, offer.Line.UnitPrice,
                offer.Quote.Currency, offer.Line.Quantity, offer.Line.Unit, offer.Line.LineTotal)).ToArray(), cheapest);
        }).OrderBy(product => product.Label, StringComparer.OrdinalIgnoreCase).ThenBy(product => product.Key, StringComparer.Ordinal).ToArray();
    }
}
