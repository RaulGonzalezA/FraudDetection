using FraudDetection.Domain;

namespace FraudDetection.Application;

public interface IFraudDetector
{
    IReadOnlySet<long> Detect(IEnumerable<Order> orders);
}
