namespace QuickOrder.Core.Application.Commands;

using QuickOrder.Core.Domain.ValueObjects;

public record NewOrderCommand(string ClOrdId, Symbol Symbol, Side Side, int Quantity, decimal Price);
