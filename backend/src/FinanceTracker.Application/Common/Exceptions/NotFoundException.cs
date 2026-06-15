namespace FinanceTracker.Application.Common.Exceptions;

/// <summary>Requested aggregate does not exist (or is not visible to the caller). Maps to HTTP 404.</summary>
public sealed class NotFoundException(string name, object key)
    : Exception($"\"{name}\" ({key}) was not found.");
