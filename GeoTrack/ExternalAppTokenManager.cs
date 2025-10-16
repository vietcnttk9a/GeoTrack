using System.Net.Http.Json;

namespace GeoTrack;

public sealed class ExternalAppTokenManager : IDisposable
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly ExternalAppConfig _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private ExternalAppToken? _currentToken;

    public ExternalAppTokenManager(HttpClient httpClient, ExternalAppConfig config)
    {
        _httpClient = httpClient;
        _config = config;
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
        Log("Logging in to external application...");

        var payload = new LoginRequest
        {
            ClientId = _config.ClientId,
            ClientSecret = _config.ClientSecret
        };

        using var response = await _httpClient.PostAsJsonAsync("api/auth-plugin/auth/login-by-key", payload, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authentication failed", DateTime.UtcNow));
            throw new InvalidOperationException($"Login failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.AccessToken))
        {
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Authentication failed", DateTime.UtcNow));
            throw new InvalidOperationException("Login response không hợp lệ.");
        }

        _currentToken = new ExternalAppToken(
            loginResponse.AccessToken,
            loginResponse.RefreshToken ?? string.Empty,
            DateTimeOffset.UtcNow.AddSeconds(loginResponse.ExpiresIn)
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
        Log("Refreshing token...");

        var payload = new RefreshRequest
        {
            RefreshToken = _currentToken.RefreshToken
        };

        using var response = await _httpClient.PostAsJsonAsync("auth/refresh", payload, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refresh failed", DateTime.UtcNow));
            throw new InvalidOperationException($"Refresh failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (refreshResponse == null || string.IsNullOrWhiteSpace(refreshResponse.AccessToken))
        {
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refresh failed", DateTime.UtcNow));
            throw new InvalidOperationException("Refresh response không hợp lệ.");
        }

        _currentToken = new ExternalAppToken(
            refreshResponse.AccessToken,
            refreshResponse.RefreshToken ?? _currentToken.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(refreshResponse.ExpiresIn)
        );

        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Token refreshed", DateTime.UtcNow));
        Log("Token refreshed successfully.");
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

    private sealed class LoginRequest
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
    }

    private sealed class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class RefreshResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
    }
}
