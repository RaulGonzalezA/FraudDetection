using FluentAssertions;
using FraudDetection.Application;
using FraudDetection.Domain;
using FraudDetection.Presentation;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace FraudDetection.Tests;

public sealed class FraudDetectionConsoleTests
{
    private const string FirstOrder = "1,1,bugs@bunny.com,123 Sesame St.,New York,NY,10011,12345689010";
    private const string SecondOrder = "2,1,elmer@fudd.com,123 Sesame St.,New York,NY,10011,10987654321";
    private const string ThirdOrder = "3,2,bugs@bunny.com,123 Sesame St.,New York,NY,10011,12345689010";

    [Fact]
    public void Uses_the_supplied_order_source_and_detector()
    {
        IOrderSource source = new InMemoryOrderSource(
            [new Order(9, 1, "bugs@example.test", new OrderAddress("12 Elm Street", "Chicago", "IL", "01234"), "001")]);
        IFraudDetector detector = new AllSourceIdsDetector();
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter();

        var exitCode = FraudDetectionConsole.Run(source, detector, output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Be("9\n");
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Writes_the_expected_result_for_the_assignment_sample()
    {
        var result = Run($"3\n{FirstOrder}\n{SecondOrder}\n{ThirdOrder}");

        result.ExitCode.Should().Be(0);
        result.Output.Should().Be("1,2\n");
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Processes_extra_orders_and_reports_the_actual_count(int declaredCount)
    {
        var result = Run($"{declaredCount}\n{FirstOrder}\n{SecondOrder}");

        result.ExitCode.Should().Be(0);
        result.Output.Should().Be("1,2\n");
        result.Error.Should().Be($"Warning: declared order count was {declaredCount}, but processed count was 2.\n");
    }

    [Fact]
    public void Logs_extra_orders_as_a_warning_with_both_counts()
    {
        using var input = new StringReader($"1\n{FirstOrder}\n{SecondOrder}");

        var log = CaptureDiagnostics(input).Should().ContainSingle().Which;

        log.Level.Should().Be(LogEventLevel.Warning);
        log.Properties["DeclaredCount"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(1);
        log.Properties["ProcessedCount"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(2L);
    }

    [Fact]
    public void Logs_invalid_input_as_an_error_with_the_public_diagnostic()
    {
        using var input = new StringReader("1");

        var log = CaptureDiagnostics(input).Should().ContainSingle().Which;

        log.Level.Should().Be(LogEventLevel.Error);
        log.Properties["Diagnostic"].Should().BeOfType<ScalarValue>().Which.Value
            .Should().Be("Line 2: expected an order record.");
    }

    [Fact]
    public void Logs_an_input_io_failure_with_a_generic_message()
    {
        using var input = new UnreadableInput();

        var log = CaptureDiagnostics(input).Should().ContainSingle().Which;

        log.Level.Should().Be(LogEventLevel.Error);
        log.RenderMessage().Should().Be("Could not read the input.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Rejects_a_duplicate_id_in_a_declared_or_additional_record(int declaredCount)
    {
        var result = Run($"{declaredCount}\n{FirstOrder}\n0{FirstOrder}");

        result.ExitCode.Should().Be(1);
        result.Output.Should().BeEmpty();
        result.Error.Should().Be("Order record 2: duplicate order ID 1.\n");
    }

    [Fact]
    public void Validates_an_additional_record_before_writing_results()
    {
        var result = Run($"1\n{FirstOrder}\n2,1,bu gs@example.test,12 Elm Street,Chicago,IL,01234,101");

        result.ExitCode.Should().Be(1);
        result.Output.Should().BeEmpty();
        result.Error.Should().Be("Line 3: invalid email address.\n");
    }

    [Fact]
    public void Writes_unique_ids_in_numeric_order_when_the_rules_overlap()
    {
        var input = """
            3
            11,1,bugs@bunny.com,12 Elm St.,Chicago,IL,01234,101
            10,1,elmer@fudd.com,12 Elm Street,Chicago,Illinois,01234,202
            2,1,b.u.gs+offers@bunny.com,22 Oak Rd.,Boston,MA,02108,303
            """;

        var result = Run(input);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Be("2,10,11\n");
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detects_interleaved_orders_by_email_and_address_regardless_of_input_order(bool reverseOrder)
    {
        string[] orders =
        [
            "20,7,bugs@bunny.com,12 Elm Street,Chicago,IL,01234,001",
            "31,7,other@example.test,99 Maple Road,Chicago,IL,01234,007",
            "40,8,roadrunner@example.test,15 Sesame St.,New York,NY,10011,003",
            "11,7,bu.gs+promo@BUNNY.COM,22 Oak Road,Chicago,IL,01234,002",
            "45,8,visitor@example.test,18 Maple Street,New York,NY,10011,008",
            "2,8,coyote@example.test,15 Sesame Street,NEW YORK,New York,10011,004"
        ];

        if (reverseOrder)
        {
            Array.Reverse(orders);
        }

        var result = Run($"{orders.Length}\n{string.Join('\n', orders)}");

        result.Should().Be((0, "2,11,20,40\n", string.Empty));
    }

    [Fact]
    public void Reads_large_numeric_ids_and_preserves_the_text_of_card_numbers()
    {
        var input = """
            2
            2147483648,2147483649,bugs@bunny.com,12 Elm St.,Chicago,IL,01234,001
            9,2147483649,bugs@bunny.com,22 Oak Rd.,Chicago,IL,01234,1
            """;

        var result = Run(input);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Be("9,2147483648\n");
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2\n" + FirstOrder + "\n" + ThirdOrder)]
    public void Writes_one_empty_line_when_no_orders_are_flagged(string input)
    {
        var result = Run(input);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Be("\n");
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("3\n" + FirstOrder + "\n" + SecondOrder)]
    [InlineData("1\n1,1,bugs@bunny.com,123 Sesame St.,New York,NY,10011")]
    [InlineData("1\norder,1,bugs@bunny.com,123 Sesame St.,New York,NY,10011,private-note")]
    [InlineData("1\n1,deal,bugs@bunny.com,123 Sesame St.,New York,NY,10011,123")]
    [InlineData("1\n1,1,invalid-email,123 Sesame St.,New York,NY,10011,123")]
    public void Reports_invalid_input_without_writing_partial_results(string input)
    {
        var result = Run(input);

        result.ExitCode.Should().Be(1);
        result.Output.Should().BeEmpty();
        result.Error.Should().NotBeNullOrWhiteSpace().And.NotContain("private-note");
    }

    [Fact]
    public void Reports_the_line_number_of_an_invalid_email_without_exposing_it()
    {
        var result = Run($"2\n{FirstOrder}\n2,1,invalid-email,12 Elm Street,Chicago,IL,01234,101");

        result.ExitCode.Should().Be(1);
        result.Output.Should().BeEmpty();
        result.Error.Should().Be("Line 3: invalid email address.\n");
    }

    private static (int ExitCode, string Output, string Error) Run(string text)
    {
        using var input = new StringReader(text);
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };

        var exitCode = FraudDetectionConsole.Run(input, output, error);

        return (exitCode, output.ToString(), error.ToString());
    }

    private static IReadOnlyList<LogEvent> CaptureDiagnostics(TextReader input)
    {
        var sink = new DiagnosticEvents();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        FraudDetectionConsole.Run(input, TextWriter.Null, logger);

        return sink.Events;
    }

    private sealed class DiagnosticEvents : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class UnreadableInput : TextReader
    {
        public override string? ReadLine() => throw new IOException("Internal read details.");
    }

    private sealed class InMemoryOrderSource(Order[] orders) : IOrderSource
    {
        public IEnumerable<Order> Read() => orders;
    }

    private sealed class AllSourceIdsDetector : IFraudDetector
    {
        public IReadOnlySet<long> Detect(IEnumerable<Order> orders) =>
            orders.Select(order => order.Id).ToHashSet();
    }
}
