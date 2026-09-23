using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Serafort.SDK.Internal;

namespace Serafort.SDK.M2M
{
    internal class TokenCache
    {
        private readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();
        private string? _token;
        private DateTime _expiresAt = DateTime.MinValue;

        public string? Get()
        {
            _cacheLock.EnterReadLock();
            try
            {
                if (DateTime.UtcNow > _expiresAt)
                    return null;
                return _token;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        public void Set(string token, int expiresInSeconds)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _token = token;
                // Proactive refresh buffer: 5 minutes
                _expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, expiresInSeconds - 300));
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }
    }

    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class M2MModule : IDisposable
    {
        private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(30);

        private readonly SerafortConfig _config;
        private readonly TokenCache _cache;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public M2MModule(SerafortConfig config, HttpClient? httpClient = null)
        {
            EndpointGuard.RequireHttps(config.Endpoint);

            _config = config;
            _cache = new TokenCache();
            _ownsHttpClient = httpClient is null;
            _httpClient = httpClient ?? new HttpClient { Timeout = DefaultHttpTimeout };

            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<string> GetAccessTokenAsync(string[]? scopes = null, CancellationToken cancellationToken = default)
        {
            var cachedToken = _cache.Get();
            if (cachedToken != null)
                return cachedToken;

            if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
                throw new InvalidOperationException("ClientId and ClientSecret are required for M2M authentication.");

            var formFields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret!)
            };

            if (scopes is { Length: > 0 })
                formFields.Add(new KeyValuePair<string, string>("scope", string.Join(' ', scopes)));

            var response = await _retryPolicy.ExecuteAsync(ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint.TrimEnd('/')}/oauth/token")
                {
                    Content = new FormUrlEncodedContent(formFields)
                };
                return _httpClient.SendAsync(request, ct);
            }, cancellationToken);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                throw new SerafortException("Failed to parse access token from the token endpoint response.");

            _cache.Set(tokenResponse.AccessToken, tokenResponse.ExpiresIn);

            return tokenResponse.AccessToken;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }
    }
}
