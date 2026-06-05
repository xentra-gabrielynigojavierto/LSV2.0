namespace Contracts;

public record ServiceResponse<T>(bool Success, T? Data, string? Error = null);
