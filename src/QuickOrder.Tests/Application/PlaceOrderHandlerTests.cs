namespace QuickOrder.Tests.Application;

using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.UseCases;
using QuickOrder.Core.Domain.ValueObjects;

public class PlaceOrderHandlerTests
{
    [Fact]
    public void ValidOrder_IsAccepted()
    {
        var book = new InMemoryOrderBook();
        var handler = new PlaceOrderHandler(book);

        var result = handler.Handle(new NewOrderCommand("C1", Symbol.PETR4, Side.Buy, 100, 10.50m));

        Assert.True(result.Accepted);
        Assert.Null(result.RejectionReason);
        Assert.Single(book.GetSnapshot());
    }

    [Fact]
    public void InvalidDomain_IsRejected_AndNotStored()
    {
        var book = new InMemoryOrderBook();
        var handler = new PlaceOrderHandler(book);

        var result = handler.Handle(new NewOrderCommand("C1", Symbol.PETR4, Side.Buy, 0, 10m));

        Assert.False(result.Accepted);
        Assert.Equal("Quantidade invalida", result.RejectionReason);
        Assert.Empty(book.GetSnapshot());
    }

    [Fact]
    public void DuplicateClOrdId_IsRejected()
    {
        var book = new InMemoryOrderBook();
        var handler = new PlaceOrderHandler(book);

        var first = handler.Handle(new NewOrderCommand("C1", Symbol.PETR4, Side.Buy, 100, 10m));
        var second = handler.Handle(new NewOrderCommand("C1", Symbol.VALE3, Side.Sell, 50, 20m));

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal("ClOrdId duplicado", second.RejectionReason);
        Assert.Single(book.GetSnapshot());
    }
}
