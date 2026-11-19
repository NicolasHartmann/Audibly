using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Audibly.Core.Metadata;

/// <summary>A search or details request that failed; the message can be shown to the user as is.</summary>
public sealed class MetadataLookupException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
///     Finds books in Audible's catalog (German and US shop) and reads their details from Audnexus.
///     Neither needs a key; both are unofficial, so a change on their side only breaks the lookup.
/// </summary>
public sealed class AudibleMetadataClient(HttpClient http)
{
    private const string SearchQuery =
        "num_results=10&products_sort_by=Relevance&response_groups=contributors,series,product_attrs,media";

    public async Task<IReadOnlyList<AudibleHit>> SearchAsync(string keywords, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return [];

        var query = $"keywords={Uri.EscapeDataString(keywords.Trim())}&{SearchQuery}";
        var de = SearchRegionAsync($"https://api.audible.de/1.0/catalog/products?{query}", "de", ct);
        var us = SearchRegionAsync($"https://api.audible.com/1.0/catalog/products?{query}", "us", ct);
        await Task.WhenAll(de, us);

        if (de.Result == null && us.Result == null)
            throw new MetadataLookupException("Audible could not be reached.");
        return AudibleJson.Merge(de.Result ?? [], us.Result ?? []);
    }

    public async Task<BookDetails> GetDetailsAsync(AudibleHit hit, CancellationToken ct)
    {
        var url = $"https://api.audnex.us/books/{Uri.EscapeDataString(hit.Asin)}?region={hit.Region}";
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new MetadataLookupException("No details found for this book.");
            return AudibleJson.ParseDetails(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException ||
                                  (e is OperationCanceledException && !ct.IsCancellationRequested))
        {
            throw new MetadataLookupException("The book details could not be loaded.", e);
        }
    }

    // one shop failing (offline, timeout, changed answer) leaves the other's hits; null means it failed
    private async Task<IReadOnlyList<AudibleHit>?> SearchRegionAsync(string url, string region, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return AudibleJson.ParseSearch(await response.Content.ReadAsStringAsync(ct), region);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
