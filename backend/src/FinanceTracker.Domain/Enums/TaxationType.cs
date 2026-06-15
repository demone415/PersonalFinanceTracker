namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Matches the numeric codes returned by ПроверкаЧека API.
/// </summary>
public enum TaxationType
{
    Osn  = 1,
    Usn  = 2,
    Envd = 4,
    Eshn = 8,
    Psn  = 16,
    Npd  = 32,
}
