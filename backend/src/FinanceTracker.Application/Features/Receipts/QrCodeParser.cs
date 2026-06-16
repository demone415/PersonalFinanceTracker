using System.Globalization;

namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Validates and parses the raw QR string of a fiscal receipt before it is
/// accepted and enqueued for fetching (T4.1.5). The expected payload is the
/// ampersand-delimited form a receipt QR encodes, e.g.
/// <c>t=20200924T1837&amp;s=349.93&amp;fn=9282440300682838&amp;i=46534&amp;fp=1273019065&amp;n=1</c>.
/// Parsing here is purely structural — it does not contact the provider.
/// </summary>
public static class QrCodeParser
{
    // QR timestamps come as yyyyMMdd'T'HHmm or yyyyMMdd'T'HHmmss (seconds optional).
    private static readonly string[] TimestampFormats =
    [
        "yyyyMMdd'T'HHmmss",
        "yyyyMMdd'T'HHmm",
    ];

    /// <summary>
    /// Attempts to parse <paramref name="raw"/>. Returns <c>true</c> and sets
    /// <paramref name="data"/> when every required field (<c>t,s,fn,i,fp,n</c>)
    /// is present and well-formed; otherwise returns <c>false</c>.
    /// </summary>
    public static bool TryParse(string? raw, out QrCodeData? data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var fields = ParseFields(raw);
        if (!fields.TryGetValue("t", out var t)
            || !fields.TryGetValue("s", out var s)
            || !fields.TryGetValue("fn", out var fn)
            || !fields.TryGetValue("i", out var i)
            || !fields.TryGetValue("fp", out var fp)
            || !fields.TryGetValue("n", out var n))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(t, TimestampFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return false;
        }

        // The sum uses a dot decimal separator regardless of server culture.
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var sum)
            || sum < 0)
        {
            return false;
        }

        if (!IsDigits(fn) || !IsDigits(fp))
        {
            return false;
        }

        if (!long.TryParse(i, NumberStyles.None, CultureInfo.InvariantCulture, out var fd) || fd <= 0)
        {
            return false;
        }

        if (!int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var operationType)
            || operationType is < 1 or > 4)
        {
            return false;
        }

        data = new QrCodeData(timestamp, sum, fn, fd, fp, operationType, raw.Trim());
        return true;
    }

    /// <summary>Same as <see cref="TryParse"/> but throws when the QR is invalid.</summary>
    public static QrCodeData Parse(string? raw) =>
        TryParse(raw, out var data) && data is not null
            ? data
            : throw new FormatException("Invalid receipt QR string.");

    private static Dictionary<string, string> ParseFields(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in raw.Trim().Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static bool IsDigits(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return value.Length > 0;
    }
}
