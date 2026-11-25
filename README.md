# Inventory Aggregation Service

A .NET 8 HTTP service that fetches paginated inventory records from the upstream Inventory API, validates dataset completeness and consistency, aggregates available stock by category, and exposes the result via an HTTP GET endpoint.

---

## Setup & Configuration

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- Visual Studio Code with the **C# Dev Kit** extension (optional)

### Configure API Base URL and API Key
Do not commit your API key to `appsettings.json`. Use **.NET User Secrets** or environment variables:

```bash
cd src/InventoryAggregator.Api
dotnet user-secrets set "InventoryApi:BaseUrl" "https://<your-assigned-base-url>"
dotnet user-secrets set "InventoryApi:ApiKey" "<your-assigned-api-key>"
# Optional: update if your assignment specifies a different header (e.g., Authorization)
dotnet user-secrets set "InventoryApi:ApiKeyHeaderName" "X-Api-Key"
```

Alternatively, pass them via environment variables:
```bash
export InventoryApi__BaseUrl="https://<your-assigned-base-url>"
export InventoryApi__ApiKey="<your-assigned-api-key>"
```

---

## Running the Application & Tests

### Run the API
```bash
dotnet run --project src/InventoryAggregator.Api
```
The service starts on `http://localhost:5080` (when using the VS Code launch profile) or the URL printed in the console.

### Call the Endpoint
```bash
curl http://localhost:5080/api/inventory/aggregated
```

### Run Unit Tests
```bash
dotnet test
```

---

## Architecture

The solution is split into two primary layers with strict separation of concerns:

1. **Upstream Client & Resilience (`Clients/InventoryApiClient.cs`):**
   - Uses `IHttpClientFactory` paired with `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`) for exponential backoff with jitter on transient HTTP failures (`5xx`, `408`, `429`, and network timeouts) while failing fast on client/auth errors (`400`, `401`, `403`).
   - Walks through all pages using `pageSize=25` (the maximum permitted) to minimize round-trips.
   - Treats `200 OK` responses defensively by verifying pagination metadata consistency across pages, rejecting truncated non-final pages, deduplicating identical records by `id`, flagging conflicting duplicate records, and confirming that the final unique record count matches `totalRecords`.
   - Translates all upstream transport, protocol, and data-integrity errors into `UpstreamApiException`, which the HTTP layer maps to a `502 Bad Gateway` `ProblemDetails` response.

2. **Domain Aggregation (`Services/InventoryAggregator.cs`):**
   - Pure, stateless business logic with zero dependency on HTTP or serialization.
   - Filters out `quantity == 0` items, groups remaining records by category, computes `totalInventoryValue` (`sum(price * quantity)`), counts available records, identifies the `mostRecentlyUpdated` record, and includes the underlying `items`.

---

## Assumptions & Known Limitations

- **Zero-stock categories:** Because records with `quantity == 0` are excluded before grouping, categories whose items all have zero stock are omitted from the response.
- **Timestamp tie-breaking:** If multiple records in a category share the exact same `lastUpdated` timestamp, the record with the higher `id` is selected deterministically for `mostRecentlyUpdated`.
- **Category normalization:** Category names are trimmed and grouped case-insensitively (`OrdinalIgnoreCase`), preserving the casing of the first encountered record.
- **In-memory buffering & caching:** All records are buffered in memory per request. Because the upstream dataset is static, adding short-lived response caching (`IMemoryCache` or output caching) would reduce upstream load and latency in production.
