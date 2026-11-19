using FraudDetection.Presentation;

using var input = Console.OpenStandardInput();
return FraudDetectionConsole.RunWithExecutionLog(input, Console.Out, Console.Error, "logs");
