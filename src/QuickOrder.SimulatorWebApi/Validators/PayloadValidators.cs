namespace QuickOrder.SimulatorWebApi.Validators;

using FluentValidation;
using QuickOrder.SimulatorWebApi.Controllers;

public class NewOrderPayloadValidator : AbstractValidator<NewOrderPayload>
{
    public NewOrderPayloadValidator()
    {
        RuleFor(x => x.ClOrdId).NotEmpty();

        RuleFor(x => x.Symbol)
            .Must(s => s == "PETR4" || s == "VALE3")
            .WithMessage("Symbol must be PETR4 or VALE3");

        RuleFor(x => x.Side)
            .Must(s => s?.ToUpperInvariant() is "BUY" or "SELL")
            .WithMessage("Side must be BUY or SELL");

        RuleFor(x => x.Qty)
            .GreaterThan(0)
            .LessThan(100_000);

        RuleFor(x => x.Price)
            .GreaterThan(0m)
            .LessThan(1000m)
            .Must(p => decimal.Round(p, 2) == p)
            .WithMessage("Price must be a multiple of 0.01");
    }
}

public class CancelOrderPayloadValidator : AbstractValidator<CancelOrderPayload>
{
    public CancelOrderPayloadValidator()
    {
        RuleFor(x => x.ClOrdId).NotEmpty();
        RuleFor(x => x.OrigClOrdId).NotEmpty();

        RuleFor(x => x.Symbol)
            .Must(s => s == "PETR4" || s == "VALE3")
            .WithMessage("Symbol must be PETR4 or VALE3");

        RuleFor(x => x.Side)
            .Must(s => s?.ToUpperInvariant() is "BUY" or "SELL")
            .WithMessage("Side must be BUY or SELL");
    }
}
