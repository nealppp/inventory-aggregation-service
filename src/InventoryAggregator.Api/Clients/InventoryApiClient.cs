using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryAggregator.Api.Configuration;
using InventoryAggregator.Api.Models;
using Microsoft.Extensions.Options;

namespace InventoryAggregator.Api.Clients;

public sealed class InventoryApiClient(
    HttpClient httpClient,
    IOptions<InventoryApiOptions> options,
    ILogger<InventoryApiClient> logger) : IInventoryApiClient
{
    private readonly InventoryApiOptions _options = options.Value;

    public async Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
    {
        if (_options.PageSize is < 1 or > 25)
            throw new InvalidOperationException($"Configured PageSize ({_options.PageSize}) must be between 1 and 25.");

        var recordsById = new Dictionary<int, InventoryRecord>();
        int currentPage = 1;
        int? expectedTotalPages = null;
        int? expectedTotalRecords = null;

        while (true)
        {
            var pageData = await FetchPageAsync(currentPage, _options.PageSize, cancellationToken);

            if (pageData.TotalPages < 0 || pageData.TotalRecords < 0)
                throw new UpstreamApiException($"Invalid pagination metadata on page {currentPage}.");

            expectedTotalPages ??= pageData.TotalPages;
            expectedTotalRecords ??= pageData.TotalRecords;

            if (pageData.TotalPages != expectedTotalPages || pageData.TotalRecords != expectedTotalRecords)
            {
                throw new UpstreamApiException(
                    $"Inconsistent pagination metadata on page {currentPage}: expected {expectedTotalRecords} records / {expectedTotalPages} pages, got {pageData.TotalRecords} / {pageData.TotalPages}.");
            }

            if (pageData.Page != currentPage)
                throw new UpstreamApiException($"Requested page {currentPage}, but upstream returned page {pageData.Page}.");

            if (expectedTotalRecords == 0)
                return [];

            if (pageData.Records is null)
                throw new UpstreamApiException($"Page {currentPage} returned a null records array.");

            bool isLastPage = currentPage >= expectedTotalPages;
            if (!isLastPage && pageData.Records.Count < _options.PageSize)
            {
                throw new UpstreamApiException(
                    $"Page {currentPage} of {expectedTotalPages} was truncated ({pageData.Records.Count} of {_options.PageSize} records).");
            }

            foreach (var record in pageData.Records)
            {
                ValidateRecord(record);

                if (recordsById.TryGetValue(record.Id, out var existing))
                {
                    if (existing != record)
                        throw new UpstreamApiException($"Conflicting duplicate record detected for ID {record.Id}.");

                    logger.LogWarning("Duplicate identical record ID {RecordId} encountered on page {Page}; deduplicating.", record.Id, currentPage);
                    continue;
                }

                recordsById[record.Id] = record;
            }

            if (isLastPage)
                break;

            currentPage++;
        }

        if (recordsById.Count != expectedTotalRecords)
        {
            throw new UpstreamApiException(
                $"Dataset completeness check failed: collected {recordsById.Count} unique records, but upstream reported {expectedTotalRecords}.");
        }

        return recordsById.Values.ToList();
    }

    private async Task<UpstreamPageResponse> FetchPageAsync(int page, int pageSize, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync($"api/records?page={page}&pageSize={pageSize}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new UpstreamApiException($"Network or timeout error fetching page {page}.", inner: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamApiException(
                    $"Upstream API returned {(int)response.StatusCode} ({response.ReasonPhrase}) on page {page}.",
                    response.StatusCode);
            }

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<UpstreamPageResponse>(cancellationToken: ct);
                return payload ?? throw new UpstreamApiException($"Empty JSON body on page {page}.");
            }
            catch (JsonException ex)
            {
                throw new UpstreamApiException($"Malformed JSON received from upstream API on page {page}.", HttpStatusCode.OK, ex);
            }
        }
    }

    private static void ValidateRecord(InventoryRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Category))
            throw new UpstreamApiException($"Record {record.Id} has an empty category.");
        if (record.Price < 0 || record.Quantity < 0)
            throw new UpstreamApiException($"Record {record.Id} has negative price or quantity.");
    }
}
