using FluentAssertions;
using FraudDetection.Application;
using FraudDetection.Domain;
using FraudDetection.Presentation;
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
