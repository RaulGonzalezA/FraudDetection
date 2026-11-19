namespace FraudDetection.Domain;

public sealed record Order(long Id, long DealId, string Email, OrderAddress Address, string CardNumber);
