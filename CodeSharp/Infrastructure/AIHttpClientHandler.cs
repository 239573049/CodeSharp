namespace CodeSharp.Infrastructure;

public class AIHttpClientHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new Exception($"AI service error: {error}");
        }

        return response;
    }
}