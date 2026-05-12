namespace QuickOrder.Core.Application.UseCases;

using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.Results;
using QuickOrder.Core.Domain.Abstractions;
using QuickOrder.Core.Domain.Entities;

public class PlaceOrderHandler
{
    private readonly IOrderBook _book;

    public PlaceOrderHandler(IOrderBook book) => _book = book;

    public PlaceOrderResult Handle(NewOrderCommand cmd)
    {
        if (!Order.TryCreate(cmd.ClOrdId, cmd.Symbol, cmd.Side, cmd.Quantity, cmd.Price, out var order, out var reason))
            return PlaceOrderResult.Reject(reason!);

        if (!_book.TryAdd(order!))
            return PlaceOrderResult.Reject("ClOrdId duplicado");

        return PlaceOrderResult.Ok();
    }
}
