using FraudDetection.Domain;

namespace FraudDetection.Application;

public sealed class FraudDetector(IIdentityNormalizer normalizer) : IFraudDetector
{
    private static readonly StringComparer IdentityTextComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly IEqualityComparer<(long DealId, string Email)> EmailKeyComparer =
        EqualityComparer<(long DealId, string Email)>.Create(
            static (left, right) => left.DealId == right.DealId
                && IdentityTextComparer.Equals(left.Email, right.Email),
            static key => HashCode.Combine(key.DealId, IdentityTextComparer.GetHashCode(key.Email)));

    private static readonly IEqualityComparer<(long DealId, OrderAddress Address)> AddressKeyComparer =
        EqualityComparer<(long DealId, OrderAddress Address)>.Create(
            static (left, right) => left.DealId == right.DealId
                && IdentityTextComparer.Equals(left.Address.Street, right.Address.Street)
                && IdentityTextComparer.Equals(left.Address.City, right.Address.City)
                && IdentityTextComparer.Equals(left.Address.State, right.Address.State)
                && StringComparer.Ordinal.Equals(left.Address.ZipCode, right.Address.ZipCode),
            static key =>
            {
                var hash = new HashCode();
                hash.Add(key.DealId);
                hash.Add(key.Address.Street, IdentityTextComparer);
                hash.Add(key.Address.City, IdentityTextComparer);
                hash.Add(key.Address.State, IdentityTextComparer);
                hash.Add(key.Address.ZipCode, StringComparer.Ordinal);

                return hash.ToHashCode();
            });

    internal static IFraudDetector Default { get; } = new FraudDetector(new IdentityNormalizer());

    public static IReadOnlySet<long> FindFraudulentOrderIds(IEnumerable<Order> orders) => Default.Detect(orders);

    public IReadOnlySet<long> Detect(IEnumerable<Order> orders)
    {
        var emailGroups = new Dictionary<(long DealId, string Email), OrderGroup>(EmailKeyComparer);
        var addressGroups = new Dictionary<(long DealId, OrderAddress Address), OrderGroup>(AddressKeyComparer);
        var fraudulentIds = new HashSet<long>();

        foreach (var order in orders)
        {
            var email = normalizer.NormalizeEmail(order.Email);
            var address = normalizer.NormalizeAddress(order.Address);
            TrackOrder(emailGroups, (order.DealId, email), order, fraudulentIds);
            TrackOrder(addressGroups, (order.DealId, address), order, fraudulentIds);
        }

        return fraudulentIds;
    }

    private static void TrackOrder<TKey>(
        Dictionary<TKey, OrderGroup> groups,
        TKey key,
        Order order,
        HashSet<long> fraudulentIds)
        where TKey : notnull
    {
        if (groups.TryGetValue(key, out var group))
        {
            group.Add(order, fraudulentIds);
        }
        else
        {
            groups.Add(key, new OrderGroup(order));
        }
    }

    private sealed class OrderGroup(Order firstOrder)
    {
        private readonly long _firstOrderId = firstOrder.Id;
        private string? _pendingCard = firstOrder.CardNumber;
        private List<long>? _additionalPendingIds;

        public void Add(Order order, HashSet<long> fraudulentIds)
        {
            if (_pendingCard is null)
            {
                fraudulentIds.Add(order.Id);
                return;
            }

            if (string.Equals(_pendingCard, order.CardNumber, StringComparison.Ordinal))
            {
                (_additionalPendingIds ??= []).Add(order.Id);
                return;
            }

            fraudulentIds.Add(_firstOrderId);
            if (_additionalPendingIds is not null)
            {
                fraudulentIds.UnionWith(_additionalPendingIds);
                _additionalPendingIds = null;
            }

            fraudulentIds.Add(order.Id);
            _pendingCard = null;
        }
    }
}
