namespace DemoFile;

/// <summary>
/// <see cref="HttpClient"/> overloads with <see cref="CancellationToken"/> that only exist in .NET 5+.
/// </summary>
internal static class HttpClientPolyfills
{
    public static async Task<byte[]> GetByteArrayAsync(this HttpClient client, string requestUri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    public static async Task<string> GetStringAsync(this HttpClient client, string requestUri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}
