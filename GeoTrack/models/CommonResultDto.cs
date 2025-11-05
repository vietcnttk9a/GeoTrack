// using System.Text.Json.Serialization;
//
// namespace GeoTrack.Models;
//
// public class CommonResultDto<T>
// {
//     [JsonPropertyName("isSuccessful")]
//     public bool IsSuccessful { get; set; }
//
//     [JsonPropertyName("data")]
//     public T? Data { get; set; }
//
//     [JsonPropertyName("errorDetail")]
//     public CommonResultDetailDto? ErrorDetail { get; set; }
//
//     [JsonPropertyName("notification")]
//     public CommonResultDetailDto? Notification { get; set; }
//
//     [JsonPropertyName("errors")]
//     public List<ValidateInputDto> Errors { get; set; } = new();
//
//     [JsonPropertyName("message")]
//     public string? Message { get; set; }
// }
//
// public class CommonResultDetailDto
// {
//     [JsonPropertyName("code")]
//     public string? Code { get; set; }
//
//     [JsonPropertyName("message")]
//     public string? Message { get; set; }
//
//     [JsonPropertyName("paramMessage")]
//     public string[]? ParamMessage { get; set; }
//
//     [JsonPropertyName("data")]
//     public object? Data { get; set; }
// }
//
// public class ValidateInputDto
// {
//     [JsonPropertyName("propertyName")]
//     public string? PropertyName { get; set; }
//
//     [JsonPropertyName("errorMessage")]
//     public string? ErrorMessage { get; set; }
//
//     [JsonPropertyName("errorCode")]
//     public string? ErrorCode { get; set; }
// }
