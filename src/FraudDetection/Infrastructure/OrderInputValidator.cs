using FluentValidation;
using FraudDetection.Domain;

namespace FraudDetection.Infrastructure;

internal sealed class OrderInputValidator : AbstractValidator<Order>
{
    internal const string RequiredFieldsMessage = "expected eight non-empty comma-separated fields.";

    public OrderInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(order => order.Email).NotEmpty().WithMessage(RequiredFieldsMessage);
        RuleFor(order => order.Address.Street).NotEmpty().WithMessage(RequiredFieldsMessage);
        RuleFor(order => order.Address.City).NotEmpty().WithMessage(RequiredFieldsMessage);
        RuleFor(order => order.Address.State).NotEmpty().WithMessage(RequiredFieldsMessage);
        RuleFor(order => order.Address.ZipCode).NotEmpty().WithMessage(RequiredFieldsMessage);
        RuleFor(order => order.CardNumber).NotEmpty().WithMessage(RequiredFieldsMessage);

        RuleFor(order => order.Email)
            .Must(email => EmailAddress.FindSeparator(email) >= 0)
            .WithMessage("invalid email address.");
    }
}
