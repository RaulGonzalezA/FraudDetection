using FraudDetection.Domain;

namespace FraudDetection.Application;

public static class FraudDetector
{
    public static IReadOnlySet<long> FindFraudulentOrderIds(IEnumerable<Order> orders)
    {
        var emailGroups = new Dictionary<(long DealId, string Email), OrderGroup>();
        var addressGroups = new Dictionary<(long DealId, OrderAddress Address), OrderGroup>();
        var fraudulentIds = new HashSet<long>();

        foreach (var order in orders)
        {
            var email = IdentityNormalizer.NormalizeEmail(order.Email);
            var address = IdentityNormalizer.NormalizeAddress(order.Address);
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
