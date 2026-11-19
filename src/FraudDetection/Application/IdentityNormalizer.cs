using System.Text.RegularExpressions;
using FraudDetection.Domain;

namespace FraudDetection.Application;

internal sealed partial class IdentityNormalizer : IIdentityNormalizer
{
    private static readonly Dictionary<string, string> StateAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["illinois"] = "il",
        ["california"] = "ca",
        ["new york"] = "ny"
    };

    public string NormalizeEmail(string email)
    {
        var separator = EmailAddress.FindSeparator(email);
        if (separator < 0)
        {
            throw new FormatException("Invalid email address.");
        }

        var localPart = email.AsSpan(0, separator);
        var plusIndex = localPart.IndexOf('+');
        if (plusIndex < 0 && !localPart.Contains('.'))
        {
            return email;
        }

        if (plusIndex >= 0)
        {
            localPart = localPart[..plusIndex];
        }

        var normalizedLocalPart = localPart.ToString().Replace(".", string.Empty, StringComparison.Ordinal);
        return string.Concat(normalizedLocalPart.AsSpan(), email.AsSpan(separator));
    }

    public OrderAddress NormalizeAddress(OrderAddress address) =>
        address with
        {
            Street = StreetAbbreviations().Replace(
                address.Street,
                static match => match.Value.Equals("st.", StringComparison.OrdinalIgnoreCase) ? "street" : "road"),
            State = StateAbbreviations.GetValueOrDefault(address.State, address.State)
        };

    [GeneratedRegex(@"(?<!\S)(?:st\.|rd\.)(?!\S)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StreetAbbreviations();
}
