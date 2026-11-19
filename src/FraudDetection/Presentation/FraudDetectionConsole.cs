using FraudDetection.Application;
using FraudDetection.Infrastructure;

namespace FraudDetection.Presentation;

public static class FraudDetectionConsole
{
    public static int Run(TextReader input, TextWriter output, TextWriter error)
    {
        IReadOnlySet<long> fraudulentIds;
        try
        {
            fraudulentIds = FraudDetector.FindFraudulentOrderIds(OrderReader.Read(input));
        }
        catch (FormatException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
        catch (IOException)
        {
            error.WriteLine("Could not read the input.");
            return 1;
        }

        FraudResultWriter.Write(fraudulentIds, output);
        return 0;
    }
}
