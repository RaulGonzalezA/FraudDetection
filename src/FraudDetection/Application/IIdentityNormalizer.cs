using FraudDetection.Domain;

namespace FraudDetection.Application;

public interface IIdentityNormalizer
{
    string NormalizeEmail(string email);

    OrderAddress NormalizeAddress(OrderAddress address);
}
