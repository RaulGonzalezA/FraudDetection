using System.Globalization;
using FraudDetection.Application;
using FraudDetection.Domain;

namespace FraudDetection.Infrastructure;

internal sealed class OrderReader(TextReader input, Action<int, long>? reportExtraOrders = null) : IOrderSource
{
    private const int FieldCount = 8;
    private static readonly OrderInputValidator InputValidator = new();

    public IEnumerable<Order> Read()
    {
        if (!int.TryParse(input.ReadLine(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaredCount)
            || declaredCount < 0)
        {
            throw new FormatException("Line 1: expected a non-negative order count.");
        }

        var processedCount = 0L;
        while (input.ReadLine() is { } line)
        {
            var order = ReadOrder(line, processedCount + 2);
            processedCount++;
            yield return order;
        }

        if (processedCount < declaredCount)
        {
            throw new FormatException($"Line {processedCount + 2}: expected an order record.");
        }

        if (processedCount > declaredCount)
        {
            reportExtraOrders?.Invoke(declaredCount, processedCount);
        }
    }

    private static Order ReadOrder(string line, long lineNumber)
    {
        var fields = line.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != FieldCount || fields[0].Length == 0 || fields[1].Length == 0)
        {
            throw new FormatException($"Line {lineNumber}: {OrderInputValidator.RequiredFieldsMessage}");
        }

        var orderId = ReadId(fields[0], "order ID", lineNumber);
        var dealId = ReadId(fields[1], "deal ID", lineNumber);
        var address = new OrderAddress(fields[3], fields[4], fields[5], fields[6]);
        var order = new Order(orderId, dealId, fields[2], address, fields[7]);
        var validation = InputValidator.Validate(order);
        if (!validation.IsValid)
        {
            throw new FormatException($"Line {lineNumber}: {validation.Errors[0].ErrorMessage}");
        }

        return order;
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
