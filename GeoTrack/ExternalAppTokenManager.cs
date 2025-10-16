using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class ExternalAppTokenManager : IDisposable
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ExternalAppConfigDto _config;
    private readonly ExternalAppHttpConfigDto _httpConfig;
    private readonly Uri _loginUri;
    private readonly Uri _refreshUri;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private ExternalAppToken? _currentToken;

    public ExternalAppTokenManager(HttpClient httpClient, ExternalAppConfigDto config)
    {
        _httpClient = httpClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Endpoints ??= new ExternalAppEndpointsConfigDto();
        _config.Http ??= new ExternalAppHttpConfigDto();
        _httpConfig = _config.Http;
        _loginUri = BuildUri(_config.BaseUrl, _config.Endpoints.LoginPath);
        _refreshUri = BuildUri(_config.BaseUrl, _config.Endpoints.RefreshPath);
    }

    public event EventHandler<ExternalAppStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;

    public async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_currentToken == null || _currentToken.IsExpired)
            {
                await LoginInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (_currentToken.ShouldRefresh())
            {
                try
                {
                    await RefreshInternalAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception refreshException)
                {
                    Log($"Token refresh failed: {refreshException.Message}");
                    _currentToken = null;
                    await LoginInternalAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return _currentToken?.AccessToken ?? throw new InvalidOperationException("Không lấy được access token.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    private async Task LoginInternalAsync(CancellationToken cancellationToken)
    {
        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authenticating", DateTime.UtcNow));

        var payload = new
        {
            clientId = _config.ClientId,
            seccretToken = _config.SeccretToken
        };

        using var response = await SendAsync(() => CreateJsonRequest(HttpMethod.Post, _loginUri, payload), cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authentication failed", DateTime.UtcNow));
            Log($"Login failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
            throw new InvalidOperationException($"Login failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CommonResultDto<TokenResponseDto>>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (result?.IsSuccessful != true || result.Data == null)
        {
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authentication failed:" + result?.Message, DateTime.UtcNow));
            Log("Login failed: response payload không hợp lệ."+ result?.Message);
            throw new InvalidOperationException("Login response không hợp lệ:"+ result?.Message);
        }

        _currentToken = new ExternalAppToken(
            result.Data.AccessToken,
            result.Data.RefreshToken ?? string.Empty,
            DateTimeOffset.UtcNow.AddSeconds(result.Data.ExpiresIn)
        );

        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authenticated", DateTime.UtcNow));
        Log("Login successful.");
    }

    private async Task RefreshInternalAsync(CancellationToken cancellationToken)
    {
        if (_currentToken == null || string.IsNullOrWhiteSpace(_currentToken.RefreshToken))
        {
            throw new InvalidOperationException("Không có refresh token để làm mới.");
        }

        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Refreshing token", DateTime.UtcNow));

        var payload = new
        {
            refresh_token = _currentToken.RefreshToken
        };

        using var response = await SendAsync(() => CreateJsonRequest(HttpMethod.Post, _refreshUri, payload), cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refresh failed", DateTime.UtcNow));
            Log($"Refresh failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
            throw new InvalidOperationException($"Refresh failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CommonResultDto<TokenResponseDto>>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (result?.IsSuccessful != true || result.Data == null)
        {
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refresh failed", DateTime.UtcNow));
            Log("Refresh failed: response payload không hợp lệ.");
            throw new InvalidOperationException("Refresh response không hợp lệ.");
        }

        _currentToken = new ExternalAppToken(
            result.Data.AccessToken,
            result.Data.RefreshToken ?? _currentToken.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(result.Data.ExpiresIn)
        );

        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refreshed", DateTime.UtcNow));
        Log("Token refreshed successfully.");
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        return await _httpClient.SendWithRetryAsync(requestFactory, _httpConfig, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, Uri uri, object payload)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ExternalAppErrorDto>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (error?.Message != null)
            {
                return error.Message;
            }
        }
        catch
        {
            // Ignore parsing errors.
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildUri(string baseUrl, string path)
    {
        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(baseUri, path ?? string.Empty);
    }

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, new LogMessageEventArgs("ExternalApp", message, DateTime.UtcNow));
    }

    private sealed record ExternalAppToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

        public bool ShouldRefresh() => DateTimeOffset.UtcNow >= ExpiresAt - RefreshThreshold;
    }
}
