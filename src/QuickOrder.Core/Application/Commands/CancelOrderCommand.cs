namespace QuickOrder.Core.Application.Commands;

public record CancelOrderCommand(string ClOrdId, string OrigClOrdId);
