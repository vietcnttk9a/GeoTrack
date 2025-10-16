using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeoTrack.Models;

namespace GeoTrack;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<DeviceConfigDto> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Không tìm thấy file cấu hình: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<DeviceConfigDto>(stream, SerializerOptions, cancellationToken);

        if (config?.Devices == null || config.Devices.Count == 0)
        {
            throw new InvalidDataException("File devices.config.json không chứa danh sách thiết bị hợp lệ.");
        }

        config.AppBehavior ??= new AppBehaviorConfigDto();
        config.AppBehavior.Tray ??= new TrayConfigDto();

        if (config.ExternalApp != null)
        {
            config.ExternalApp.Endpoints ??= new ExternalAppEndpointsConfigDto();
            config.ExternalApp.Http ??= new ExternalAppHttpConfigDto();
        }

        return config;
    }
}
