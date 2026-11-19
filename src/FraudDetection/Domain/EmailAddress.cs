namespace FraudDetection.Domain;

internal static class EmailAddress
{
    public static int FindSeparator(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 0 || separator == email.Length - 1 || email.AsSpan(separator + 1).Contains('@'))
        {
            return -1;
        }

        foreach (var character in email)
        {
            if (char.IsWhiteSpace(character))
            {
                return -1;
            }
        }

        return separator;
    }
}
