using System.Text.Json.Serialization;

namespace ManaDuitnya.Api.Dtos;

public class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public object? Details { get; set; }
}

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Meta { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Ok(T data, object meta, string message = "") =>
        new() { Success = true, Message = message, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string message, string code = "ERROR", object? details = null) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            Error = new ApiError { Code = code, Details = details }
        };
}

public class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; set; }

    public static ApiResponse Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, string code = "ERROR", object? details = null) =>
        new()
        {
            Success = false,
            Message = message,
            Error = new ApiError { Code = code, Details = details }
        };
}

