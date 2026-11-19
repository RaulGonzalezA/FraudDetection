using FraudDetection.Domain;

namespace FraudDetection.Application;

public interface IOrderSource
{
    IEnumerable<Order> Read();
}
