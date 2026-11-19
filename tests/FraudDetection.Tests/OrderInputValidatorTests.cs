using FluentAssertions;
using FraudDetection.Domain;
using FraudDetection.Infrastructure;
using Xunit;

namespace FraudDetection.Tests;

public sealed class OrderInputValidatorTests
{
    private static readonly Order ValidOrder = new(
        9, 2, "bu.gs+offers@example.test", new OrderAddress("12 Elm Street", "Chicago", "IL", "01234"), "001");

    private readonly OrderInputValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_order_with_textual_zip_and_card_identifiers()
    {
        var result = _validator.Validate(ValidOrder);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.test")]
    [InlineData("customer@")]
    [InlineData("customer@@example.test")]
    public void Rejects_an_email_with_an_invalid_separator(string email)
    {
        var result = _validator.Validate(ValidOrder with { Email = email });

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(Order.Email));
        error.ErrorMessage.Should().Be("invalid email address.");
    }
}
