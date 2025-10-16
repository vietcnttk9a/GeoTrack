using System.Text.Json;

namespace GeoTrack;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<DeviceConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Không tìm thấy file cấu hình: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<DeviceConfig>(stream, SerializerOptions, cancellationToken);

        if (config?.Devices == null || config.Devices.Count == 0)
        {
            throw new InvalidDataException("File devices.config.json không chứa danh sách thiết bị hợp lệ.");
        }

        return config;
    }
}
