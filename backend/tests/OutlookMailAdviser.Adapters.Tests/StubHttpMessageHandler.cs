namespace OutlookMailAdviser.Adapters.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    private int _requestCount;

    public IList<string> RequestBodies { get; } = new List<string>();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        var requestNumber = Interlocked.Increment(ref _requestCount);
        return responseFactory(request, requestNumber);
    }
}

