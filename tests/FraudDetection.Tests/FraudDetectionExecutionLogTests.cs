using FluentAssertions;
using FraudDetection.Presentation;
using Xunit;

namespace FraudDetection.Tests;

public sealed class FraudDetectionExecutionLogTests
{
    [Fact]
    public void Creates_a_separate_timestamped_log_for_each_successful_execution()
    {
        using var folder = new TemporaryLogDirectory();

        var first = Run("0", folder.Path);
        var second = Run("0", folder.Path);

        first.Should().Be((0, "\n", string.Empty));
        second.Should().Be((0, "\n", string.Empty));
        var logs = Directory.GetFiles(folder.Path, "*.log");
        logs.Should().HaveCount(2);

        foreach (var log in logs)
        {
            Path.GetFileName(log).Should().MatchRegex(@"^fraud-detection-\d{8}T\d{9}Z-[0-9a-f]{32}\.log$");
            File.ReadAllText(log).Should()
                .Contain(" INF] Fraud detection started.")
                .And.Contain(" INF] Fraud detection finished with exit code 0.");
        }
    }

    [Fact]
    public void Writes_excess_rows_as_a_warning_to_the_log_and_stderr()
    {
        using var folder = new TemporaryLogDirectory();
        const string input = """
            1
            1,1,bugs@example.test,12 Elm Street,Chicago,IL,01234,001
            2,1,bugs@example.test,18 Oak Road,Chicago,IL,01234,002
            """;
        const string warning = "Warning: declared order count was 1, but processed count was 2.";

        var result = Run(input, folder.Path);

        result.Should().Be((0, "1,2\n", warning + "\n"));
        ReadSingleLog(folder.Path).Should().Contain(" WRN] " + warning);
    }

    [Fact]
    public void Records_the_error_and_exit_code_of_a_failed_execution()
    {
        using var folder = new TemporaryLogDirectory();
        const string input = "1\n1,1,invalid-email,12 Elm Street,Chicago,IL,01234,test-card-data";

        var result = Run(input, folder.Path);

        result.Should().Be((1, string.Empty, "Line 2: invalid email address.\n"));
        ReadSingleLog(folder.Path).Should()
            .Contain(" ERR] Line 2: invalid email address.")
            .And.Contain(" INF] Fraud detection finished with exit code 1.")
            .And.NotContain("invalid-email")
            .And.NotContain("test-card-data");
    }

    [Fact]
    public void Reports_an_unavailable_log_directory_and_keeps_processing()
    {
        using var folder = new TemporaryLogDirectory();
        var path = Path.Combine(folder.Path, "existing-file");
        File.WriteAllText(path, "existing content");

        var result = Run("0", path);

        result.Should().Be((0, "\n", $"Warning: could not create the execution log in {path}.\n"));
        File.ReadAllText(path).Should().Be("existing content");
    }

    private static string ReadSingleLog(string directory)
    {
        var log = Directory.GetFiles(directory, "*.log").Should().ContainSingle().Which;
        return File.ReadAllText(log);
    }

    private static (int ExitCode, string Output, string Error) Run(string text, string logDirectory)
    {
        using var input = new StringReader(text);
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        var exitCode = FraudDetectionConsole.RunWithExecutionLog(input, output, error, logDirectory);

        return (exitCode, output.ToString(), error.ToString());
    }

    private sealed class TemporaryLogDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("fraud-detection-logs-");

        public string Path => _directory.FullName;

        public void Dispose()
        {
            foreach (var file in _directory.GetFiles())
            {
                file.Delete();
            }

            _directory.Delete();
        }
    }
}
