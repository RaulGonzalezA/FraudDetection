using FluentAssertions;
using FraudDetection.Application;
using FraudDetection.Domain;
using Xunit;

namespace FraudDetection.Tests;

public sealed class IdentityNormalizerTests
{
    private readonly IIdentityNormalizer _normalizer = new IdentityNormalizer();

    [Theory]
    [InlineData("BUGS@BuNnY.com", "BUGS@BuNnY.com")]
    [InlineData("bu.gs@bun.ny.com", "bugs@bun.ny.com")]
    [InlineData("bugs+orders@BUNNY.COM", "bugs@BUNNY.COM")]
    [InlineData("b.u.gs+campaign.more@bun.ny.com", "bugs@bun.ny.com")]
    public void Normalizes_only_the_supported_parts_of_an_email(string email, string expected)
    {
        _normalizer.NormalizeEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.test")]
    [InlineData("customer@")]
    [InlineData("customer@@example.test")]
    public void Rejects_an_email_without_one_separator_between_non_empty_parts(string email)
    {
        Action normalize = () => _normalizer.NormalizeEmail(email);

        normalize.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("123 Sesame St.", "iLLINOIS", "123 Sesame street", "il")]
    [InlineData("18 Oak RD.", "CALIFORNIA", "18 Oak road", "ca")]
    [InlineData("18 Crest.", "NEW YORK", "18 Crest.", "ny")]
    [InlineData("18 Broadway", "MA", "18 Broadway", "MA")]
    public void Expands_only_complete_street_abbreviations_and_maps_the_supported_states(
        string street,
        string state,
        string expectedStreet,
        string expectedState)
    {
        var address = new OrderAddress(street, "Some City", state, "01234");

        var normalized = _normalizer.NormalizeAddress(address);

        normalized.Should().Be(new OrderAddress(expectedStreet, "Some City", expectedState, "01234"));
    }
}
