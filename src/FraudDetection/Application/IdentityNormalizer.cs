using System.Text.RegularExpressions;
using FraudDetection.Domain;

namespace FraudDetection.Application;

internal static partial class IdentityNormalizer
{
    public static string NormalizeEmail(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 0 || separator == email.Length - 1 || email.AsSpan(separator + 1).Contains('@'))
        {
            throw new FormatException("Invalid email address.");
        }

        var localPart = email.AsSpan(0, separator);
        var plusIndex = localPart.IndexOf('+');
        if (plusIndex < 0 && !localPart.Contains('.'))
        {
            return email.ToLowerInvariant();
        }

        if (plusIndex >= 0)
        {
            localPart = localPart[..plusIndex];
        }

        var normalizedLocalPart = localPart.ToString().Replace(".", string.Empty, StringComparison.Ordinal);
        return string.Concat(normalizedLocalPart.AsSpan(), email.AsSpan(separator)).ToLowerInvariant();
    }

    public static OrderAddress NormalizeAddress(OrderAddress address) =>
        address with
        {
            Street = StreetAbbreviations().Replace(
                address.Street.ToLowerInvariant(),
                static match => match.Value == "st." ? "street" : "road"),
            City = address.City.ToLowerInvariant(),
            State = NormalizeState(address.State)
        };

    private static string NormalizeState(string state)
    {
        var normalized = state.ToLowerInvariant();
        return normalized switch
        {
            "illinois" => "il",
            "california" => "ca",
            "new york" => "ny",
            _ => normalized
        };
    }

    [GeneratedRegex(@"(?<!\S)(?:st\.|rd\.)(?!\S)")]
    private static partial Regex StreetAbbreviations();
}
