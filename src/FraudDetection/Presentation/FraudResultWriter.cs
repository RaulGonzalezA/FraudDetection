using System.Globalization;

namespace FraudDetection.Presentation;

internal static class FraudResultWriter
{
    public static void Write(IEnumerable<long> orderIds, TextWriter output)
    {
        var sortedIds = orderIds.Order().Select(static id => id.ToString(CultureInfo.InvariantCulture));
        output.WriteLine(string.Join(",", sortedIds));
    }
}
