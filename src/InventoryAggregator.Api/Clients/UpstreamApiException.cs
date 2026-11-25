using System.Net;

namespace InventoryAggregator.Api.Clients;

public sealed class UpstreamApiException(
    string message,
    HttpStatusCode? upstreamStatus = null,
    Exception? inner = null) : Exception(message, inner)
{
    public HttpStatusCode? UpstreamStatus { get; } = upstreamStatus;
}
