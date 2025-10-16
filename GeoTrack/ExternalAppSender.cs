using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeoTrack;

public sealed class ExternalAppSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ExternalAppConfig _config;
    private readonly ExternalAppTokenManager _tokenManager;
    private readonly Func<object> _payloadFactory;
    private readonly TimeSpan _interval;

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public ExternalAppSender(HttpClient httpClient, ExternalAppConfig config, ExternalAppTokenManager tokenManager, Func<object> payloadFactory)
    {
        _httpClient = httpClient;
        _config = config;
        _tokenManager = tokenManager;
        _payloadFactory = payloadFactory;
        var seconds = config.SendIntervalSeconds <= 0 ? 5 : config.SendIntervalSeconds;
        _interval = TimeSpan.FromSeconds(seconds);
    }

    public event EventHandler<ExternalAppStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;

    public void Start()
    {
        if (_backgroundTask != null && !_backgroundTask.IsCompleted)
        {
            throw new InvalidOperationException("Sender is already running.");
        }

        _cts = new CancellationTokenSource();
        _backgroundTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();

        if (_backgroundTask != null)
        {
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
        }

        _cts.Dispose();
        _cts = null;
        _backgroundTask = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
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

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/golf/check-location/telemetry");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(payload, options: SerializerOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Telemetry sent", DateTime.UtcNow));
                Log("Telemetry sent successfully.");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                StatusChanged?.Invoke(this, new ExternalAppStatusChangedEventArgs("Telemetry failed", DateTime.UtcNow));
                Log($"Telemetry failed: {(int)response.StatusCode} {response.ReasonPhrase} {content}");
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

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, new LogMessageEventArgs("ExternalApp", message, DateTime.UtcNow));
    }
}
