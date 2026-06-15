namespace FinanceTracker.Application.Common.Exceptions;

/// <summary>Caller is authenticated but not allowed to perform the action. Maps to HTTP 403.</summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);
