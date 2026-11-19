using FraudDetection.Application;
using FraudDetection.Infrastructure;

namespace FraudDetection.Presentation;

public static class FraudDetectionConsole
{
    public static int Run(TextReader input, TextWriter output, TextWriter error) =>
        Run(new OrderReader(input), FraudDetector.Default, output, error);

    public static int Run(IOrderSource source, IFraudDetector detector, TextWriter output, TextWriter error)
    {
        IReadOnlySet<long> fraudulentIds;
        try
        {
            fraudulentIds = detector.Detect(source.Read());
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
