namespace FinanceTracker.Application.Common.Exceptions;

/// <summary>
/// The request is well-formed but violates a business rule that depends on server
/// state and so cannot be expressed in a stateless FluentValidation validator —
/// e.g. a foreign-currency accrual submitted without an exchange rate. Maps to
/// HTTP 400.
/// </summary>
public sealed class ValidationException(string message) : Exception(message);
