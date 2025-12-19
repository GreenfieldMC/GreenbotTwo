using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreenbotTwo.Configuration.Models.Endpoints;
using Microsoft.Extensions.Options;

namespace GreenbotTwo.Services.Credentials;

public class OAuthClientCredentialsHandler(IOptions<ApiEndpointSettings> options) : DelegatingHandler
{
    private readonly ApiEndpointSettings _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (NeedsToken())
        {
            await RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool NeedsToken()
    {
        if (string.IsNullOrEmpty(_accessToken)) return true;
        return DateTimeOffset.UtcNow >= _expiresAt;
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!NeedsToken()) return;

            using var req = new HttpRequestMessage(HttpMethod.Post, _options.GreenfieldCoreApi.TokenEndpoint);
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.GreenfieldCoreApi.ClientId,
                ["client_secret"] = _options.GreenfieldCoreApi.ClientSecret
            };

            req.Content = new FormUrlEncodedContent(body);
            using var client = new HttpClient();
            var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: ct)
                        ?? throw new InvalidOperationException("Token response parse failed");

            _accessToken = token.AccessToken;
            var expiresIn = token.ExpiresIn;
            var skew = _options.GreenfieldCoreApi.RefreshSkewSeconds;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - skew);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to refresh OAuth token", e);
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}