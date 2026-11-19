using FluentAssertions;
using FraudDetection.Application;
using FraudDetection.Domain;
using Xunit;

namespace FraudDetection.Tests;

public sealed class FraudDetectorTests
{
    private static readonly OrderAddress SharedAddress = new("12 Elm Street", "Chicago", "IL", "01234");

    [Fact]
    public void Finds_both_orders_when_email_and_deal_match_but_cards_differ()
    {
        var orders = new[]
        {
            CreateOrder(1, email: "bugs@bunny.com", card: "101"),
            CreateOrder(2, email: "bugs@bunny.com", card: "202")
        };

        var fraudulentIds = FraudDetector.FindFraudulentOrderIds(orders);

        fraudulentIds.Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public void Uses_the_supplied_identity_normalizer()
    {
        IFraudDetector detector = new FraudDetector(new SharedEmailNormalizer());
        var orders = new[] { CreateOrder(1, card: "101"), CreateOrder(2, card: "202") };

        detector.Detect(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public void Finds_both_orders_when_address_and_deal_match_but_cards_and_emails_differ()
    {
        var orders = new[]
        {
            CreateOrder(1, address: SharedAddress, card: "101"),
            CreateOrder(2, address: SharedAddress, card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ignores_orders_that_use_the_same_card_in_a_group(bool groupByEmail)
    {
        var orders = new[] { "101", "101" }
            .Select((card, index) => CreateGroupedOrder(index + 1, card, groupByEmail));

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEmpty();
    }

    [Fact]
    public void Keeps_orders_with_the_same_identity_in_different_deals_separate()
    {
        var orders = new[]
        {
            CreateOrder(1, dealId: 1, email: "bugs@bunny.com", address: SharedAddress, card: "101"),
            CreateOrder(2, dealId: 2, email: "bugs@bunny.com", address: SharedAddress, card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    public void Rejects_a_repeat_order_id_even_across_deals(long secondDealId)
    {
        var orders = new[]
        {
            CreateOrder(9, dealId: 1, email: "bugs@bunny.com", card: "101"),
            CreateOrder(9, dealId: secondDealId, email: "bugs@bunny.com", card: "202")
        };
        Action detect = () => FraudDetector.FindFraudulentOrderIds(orders);

        detect.Should().Throw<FormatException>()
            .Which.Message.Should().Be("Order record 2: duplicate order ID 9.");
    }

    [Theory]
    [InlineData("bugs@bunny.com", "BuGS@BuNNy.COM")]
    [InlineData("bugs1@bunny.com", "bugs.1@bunny.com")]
    [InlineData("bugs@bunny.com", "bugs+10@bunny.com")]
    [InlineData("bugs1@bunny.com", "b.u.gs.1+campaign.code@BUNNY.COM")]
    [InlineData("\u03A3@example.test", "\u03C2@example.test")]
    public void Matches_supported_email_variants(string firstEmail, string secondEmail)
    {
        var orders = new[]
        {
            CreateOrder(1, email: firstEmail, card: "101"),
            CreateOrder(2, email: secondEmail, card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public void Preserves_dots_in_the_email_domain()
    {
        var orders = new[]
        {
            CreateOrder(1, email: "bugs@bunny.com", card: "101"),
            CreateOrder(2, email: "bugs@bun.ny.com", card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEmpty();
    }

    [Theory]
    [InlineData("12 ELM STREET", "CHICAGO", "IL", "12 Elm Street", "Chicago", "il")]
    [InlineData("12 Elm St.", "Chicago", "Illinois", "12 Elm Street", "Chicago", "IL")]
    [InlineData("12 Elm Rd.", "San Francisco", "CA", "12 Elm Road", "San Francisco", "California")]
    [InlineData("12 Elm Road", "New York", "New York", "12 ELM ROAD", "NEW YORK", "NY")]
    [InlineData("12 Elm Street", "\u03A3", "IL", "12 Elm Street", "\u03C2", "IL")]
    public void Matches_supported_address_variants(
        string firstStreet,
        string firstCity,
        string firstState,
        string secondStreet,
        string secondCity,
        string secondState)
    {
        var orders = new[]
        {
            CreateOrder(1, address: new OrderAddress(firstStreet, firstCity, firstState, "01234"), card: "101"),
            CreateOrder(2, address: new OrderAddress(secondStreet, secondCity, secondState, "01234"), card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Theory]
    [InlineData("14 Elm Street", "Chicago", "IL", "01234")]
    [InlineData("12 Elm Street", "Springfield", "IL", "01234")]
    [InlineData("12 Elm Street", "Chicago", "CA", "01234")]
    [InlineData("12 Elm Street", "Chicago", "IL", "1234")]
    public void Requires_all_four_address_fields_to_match(string street, string city, string state, string zipCode)
    {
        var orders = new[]
        {
            CreateOrder(1, address: SharedAddress, card: "101"),
            CreateOrder(2, address: new OrderAddress(street, city, state, zipCode), card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEmpty();
    }

    [Fact]
    public void Expands_street_abbreviations_only_as_complete_tokens()
    {
        var orders = new[]
        {
            CreateOrder(1, address: SharedAddress with { Street = "18 Crest." }, card: "101"),
            CreateOrder(2, address: SharedAddress with { Street = "18 Crestreet" }, card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Marks_all_orders_in_a_group_once_a_different_card_appears(bool groupByEmail)
    {
        var orders = new[] { "101", "101", "202", "101" }
            .Select((card, index) => CreateGroupedOrder(index + 1, card, groupByEmail));

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Returns_each_id_once_when_the_rules_overlap()
    {
        var orders = new[]
        {
            CreateOrder(11, email: "bugs@bunny.com", address: SharedAddress, card: "101"),
            CreateOrder(10, address: SharedAddress, card: "202"),
            CreateOrder(2, email: "bugs@bunny.com", card: "303")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 2, 10, 11 });
    }

    [Fact]
    public void Does_not_propagate_fraud_to_a_related_group_with_one_card()
    {
        var orders = new[]
        {
            CreateOrder(1, email: "bugs@bunny.com", card: "101"),
            CreateOrder(2, email: "bugs@bunny.com", address: SharedAddress, card: "202"),
            CreateOrder(3, address: SharedAddress, card: "202")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public void Preserves_leading_zeroes_when_comparing_card_numbers()
    {
        var orders = new[]
        {
            CreateOrder(1, email: "bugs@bunny.com", card: "001"),
            CreateOrder(2, email: "bugs@bunny.com", card: "1")
        };

        FraudDetector.FindFraudulentOrderIds(orders).Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    private static Order CreateGroupedOrder(long id, string card, bool groupByEmail) =>
        CreateOrder(
            id,
            email: groupByEmail ? "bugs@bunny.com" : null,
            address: groupByEmail ? null : SharedAddress,
            card: card);

    private static Order CreateOrder(
        long id,
        long dealId = 1,
        string? email = null,
        OrderAddress? address = null,
        string card = "101") =>
        new(id, dealId, email ?? $"customer{id}@example.test",
            address ?? new OrderAddress($"{id} Elm Street", "Chicago", "IL", "01234"), card);

    private sealed class SharedEmailNormalizer : IIdentityNormalizer
    {
        public string NormalizeEmail(string email) => "shared@example.test";

        public OrderAddress NormalizeAddress(OrderAddress address) => address;
    }
}
