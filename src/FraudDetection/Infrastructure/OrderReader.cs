using System.Globalization;
using FraudDetection.Domain;

namespace FraudDetection.Infrastructure;

internal static class OrderReader
{
    private const int FieldCount = 8;

    public static IEnumerable<Order> Read(TextReader input)
    {
        if (!int.TryParse(input.ReadLine(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || count < 0)
        {
            throw new FormatException("Line 1: expected a non-negative order count.");
        }

        for (var index = 0; index < count; index++)
        {
            var lineNumber = index + 2L;
            var line = input.ReadLine()
                ?? throw new FormatException($"Line {lineNumber}: expected an order record.");

            yield return ReadOrder(line, lineNumber);
        }
    }

    private static Order ReadOrder(string line, long lineNumber)
    {
        var fields = line.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != FieldCount || fields.Any(string.IsNullOrEmpty))
        {
            throw new FormatException($"Line {lineNumber}: expected eight non-empty comma-separated fields.");
        }

        var orderId = ReadId(fields[0], "order ID", lineNumber);
        var dealId = ReadId(fields[1], "deal ID", lineNumber);
        var address = new OrderAddress(fields[3], fields[4], fields[5], fields[6]);

        return new Order(orderId, dealId, fields[2], address, fields[7]);
    }

    private static long ReadId(string value, string field, long lineNumber)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            throw new FormatException($"Line {lineNumber}: invalid {field}.");
        }

        return id;
    }
}
