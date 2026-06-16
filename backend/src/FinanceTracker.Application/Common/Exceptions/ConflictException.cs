namespace FinanceTracker.Application.Common.Exceptions;

/// <summary>The request conflicts with the current state (e.g. a duplicate). Maps to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
