namespace QuickOrder.Core.Application.UseCases;

using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.Results;
using QuickOrder.Core.Domain.Abstractions;

public class CancelOrderHandler
{
    private readonly IOrderBook _book;

    public CancelOrderHandler(IOrderBook book) => _book = book;

    public CancelOrderResult Handle(CancelOrderCommand cmd)
    {
        if (_book.TryRemove(cmd.OrigClOrdId))
            return CancelOrderResult.Ok();
        return CancelOrderResult.Reject("ordem nao encontrada");
    }
}
