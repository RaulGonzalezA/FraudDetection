using FluentAssertions;
using FraudDetection.Application;
using FraudDetection.Domain;
using FraudDetection.Infrastructure;
using Xunit;

namespace FraudDetection.Tests;

public sealed class OrderReaderTests
{
    private const string ValidOrder = "9,2,bugs@example.test,12 Elm Street,Chicago,IL,01234,001";
    private const string HeaderError = "Line 1: expected a non-negative order count.";
    private const string FieldsError = "Line 2: expected eight non-empty comma-separated fields.";

    [Fact]
    public void Reads_the_declared_orders_in_input_order_and_preserves_text_fields()
    {
        var orders = ReadOrders("""
            2
            9, 2, bugs@example.test, 12 Elm Street, Chicago, IL, 01234, 001
            10,2,elmer@example.test,18 Oak Road,Boston,MA,02108,1
            """);

        orders.Should().Equal(
            new Order(9, 2, "bugs@example.test", new OrderAddress("12 Elm Street", "Chicago", "IL", "01234"), "001"),
            new Order(10, 2, "elmer@example.test", new OrderAddress("18 Oak Road", "Boston", "MA", "02108"), "1"));
    }

    [Fact]
    public void Returns_no_orders_when_the_count_is_zero()
    {
        ReadOrders("0").Should().BeEmpty();
    }

    [Theory]
    [InlineData("", HeaderError)]
    [InlineData("-1", HeaderError)]
    [InlineData("two", HeaderError)]
    [InlineData("2\n" + ValidOrder, "Line 3: expected an order record.")]
    [InlineData("1\n9,2,bugs@example.test,12 Elm Street,Chicago,IL,01234", FieldsError)]
    [InlineData("1\n" + ValidOrder + ",extra", FieldsError)]
    [InlineData("1\ninvalid,2,bugs@example.test,12 Elm Street,Chicago,IL,01234,001", "Line 2: invalid order ID.")]
    [InlineData("1\n9,invalid,bugs@example.test,12 Elm Street,Chicago,IL,01234,001", "Line 2: invalid deal ID.")]
    [InlineData("1\n9,2,@example.test,12 Elm Street,Chicago,IL,01234,001", "Line 2: invalid email address.")]
    public void Reports_the_line_and_reason_for_invalid_input(string input, string expectedMessage)
    {
        Action read = () => ReadOrders(input);

        read.Should().Throw<FormatException>().Which.Message.Should().Be(expectedMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Rejects_an_empty_value_in_any_of_the_eight_fields(int fieldIndex)
    {
        var fields = ValidOrder.Split(',');
        fields[fieldIndex] = " ";
        Action read = () => ReadOrders("1\n" + string.Join(',', fields));

        read.Should().Throw<FormatException>().Which.Message.Should().Be(FieldsError);
    }

    private static Order[] ReadOrders(string text)
    {
        using var input = new StringReader(text);
        IOrderSource source = new OrderReader(input);

        return source.Read().ToArray();
    }
}
