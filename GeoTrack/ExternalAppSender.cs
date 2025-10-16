using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class ExternalAppSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ExternalAppConfigDto _config;
    private readonly ExternalAppHttpConfigDto _httpConfig;
    private readonly ExternalAppTokenManager _tokenManager;
    private readonly Func<AggregatePayloadDto> _payloadFactory;
    private readonly Uri _aggregateUri;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _pauseGate = new(0, 1);

    private volatile bool _isPaused;
    private Task? _backgroundTask;

    public ExternalAppSender(
        HttpClient httpClient,
        ExternalAppConfigDto config,
        ExternalAppTokenManager tokenManager,
        Func<AggregatePayloadDto> payloadFactory)
    {
        _httpClient = httpClient;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Endpoints ??= new ExternalAppEndpointsConfigDto();
        _config.Http ??= new ExternalAppHttpConfigDto();
        _httpConfig = _config.Http;
        _tokenManager = tokenManager;
        _payloadFactory = payloadFactory;
        _aggregateUri = BuildUri(_config.BaseUrl, _config.Endpoints.AggregatePath);
        var seconds = _config.SendIntervalSeconds <= 0 ? 5 : _config.SendIntervalSeconds;
        _interval = TimeSpan.FromSeconds(seconds);
    }

    public event EventHandler<ExternalAppStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;

    public Task? Completion => _backgroundTask;

    public bool IsPaused => _isPaused;

    public void Start(CancellationToken cancellationToken)
    {
        if (_backgroundTask != null && !_backgroundTask.IsCompleted)
        {
            throw new InvalidOperationException("Sender is already running.");
        }

        _backgroundTask = Task.Run(() => RunAsync(cancellationToken), cancellationToken);
    }

    public void Pause()
    {
        _isPaused = true;
        while (_pauseGate.CurrentCount > 0)
        {
            _pauseGate.Wait(0);
        }
        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Paused", DateTime.UtcNow));
    }

    public void Resume()
    {
        if (!_isPaused)
        {
            return;
        }

        _isPaused = false;
        if (_pauseGate.CurrentCount == 0)
        {
            _pauseGate.Release();
        }
        StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Resumed", DateTime.UtcNow));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await AttemptSendAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task AttemptSendAsync(CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await _tokenManager.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = _payloadFactory();

            using var response = await _httpClient.SendWithRetryAsync(() => CreateRequest(accessToken, payload), _httpConfig, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Telemetry sent", DateTime.UtcNow));
                Log("Telemetry sent successfully.");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Telemetry failed", DateTime.UtcNow));
                Log($"Telemetry failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Telemetry error", DateTime.UtcNow));
            Log($"Telemetry error: {ex.Message}");
        }
    }

    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        while (_isPaused && !cancellationToken.IsCancellationRequested)
        {
            await _pauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private HttpRequestMessage CreateRequest(string accessToken, AggregatePayloadDto payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _aggregateUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload, options: SerializerOptions);
        return request;
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
}
