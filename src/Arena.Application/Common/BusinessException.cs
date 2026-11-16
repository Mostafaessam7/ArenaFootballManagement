namespace Arena.Application.Common;

/// <summary>A broken business rule. The API turns it into ProblemDetails with <see cref="StatusCode"/>.</summary>
public sealed class BusinessException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
