using System.Globalization;
using System.Text;
using FraudDetection.Application;
using FraudDetection.Infrastructure;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace FraudDetection.Presentation;

public static class FraudDetectionConsole
{
    internal static int RunWithExecutionLog(Stream input, TextWriter output, TextWriter error, string logDirectory)
    {
        // Match the optional UTF-8 preamble while keeping the strict UTF-8 decoder.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
        using var reader = new StreamReader(input, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return RunWithExecutionLog(reader, output, error, logDirectory);
    }

    internal static int RunWithExecutionLog(TextReader input, TextWriter output, TextWriter error, string logDirectory)
    {
        using var logFile = TryOpenExecutionLog(logDirectory);
        using var logger = CreateDiagnosticLogger(error, logFile);

        if (logFile is null)
        {
            logger.Warning("Warning: could not create the execution log in {LogDirectory:l}.", logDirectory);
        }

        logger.Information("Fraud detection started.");
        var exitCode = Run(input, output, logger);
        logger.Information("Fraud detection finished with exit code {ExitCode}.", exitCode);

        return exitCode;
    }

    public static int Run(TextReader input, TextWriter output, TextWriter error)
    {
        using var logger = CreateDiagnosticLogger(error);
        return Run(input, output, logger);
    }

    internal static int Run(TextReader input, TextWriter output, ILogger logger)
    {
        var source = new OrderReader(input, (declaredCount, processedCount) =>
            logger.Warning(
                "Warning: declared order count was {DeclaredCount}, but processed count was {ProcessedCount}.",
                declaredCount,
                processedCount));

        return Run(source, FraudDetector.Default, output, logger);
    }

    public static int Run(IOrderSource source, IFraudDetector detector, TextWriter output, TextWriter error)
    {
        using var logger = CreateDiagnosticLogger(error);
        return Run(source, detector, output, logger);
    }

    private static int Run(IOrderSource source, IFraudDetector detector, TextWriter output, ILogger logger)
    {
        IReadOnlySet<long> fraudulentIds;
        try
        {
            fraudulentIds = detector.Detect(source.Read());
        }
        catch (FormatException exception)
        {
            logger.Error("{Diagnostic:l}", exception.Message);
            return 1;
        }
        catch (DecoderFallbackException)
        {
            logger.Error("Input is not valid UTF-8.");
            return 1;
        }
        catch (IOException)
        {
            logger.Error("Could not read the input.");
            return 1;
        }

        try
        {
            FraudResultWriter.Write(fraudulentIds, output);
            output.Flush();
        }
        catch (IOException)
        {
            logger.Error("Could not write the result.");
            return 1;
        }

        return 0;
    }

    private static Logger CreateDiagnosticLogger(TextWriter error, TextWriter? logFile = null)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.TextWriter(new DiagnosticFormatter(), error, restrictedToMinimumLevel: LogEventLevel.Warning);

        if (logFile is not null)
        {
            configuration.WriteTo.TextWriter(
                logFile,
                outputTemplate: "[{Timestamp:yyyy-MM-dd'T'HH:mm:ss.fffzzz} {Level:u3}] {Message:lj}{NewLine}",
                formatProvider: CultureInfo.InvariantCulture);
        }

        return configuration.CreateLogger();
    }

    private static StreamWriter? TryOpenExecutionLog(string logDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            var logPath = Path.Combine(logDirectory, $"fraud-detection-{timestamp}-{Guid.NewGuid():N}.log");

            return new StreamWriter(File.Open(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class DiagnosticFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            logEvent.RenderMessage(output, CultureInfo.InvariantCulture);
            output.WriteLine();
        }
    }
}
