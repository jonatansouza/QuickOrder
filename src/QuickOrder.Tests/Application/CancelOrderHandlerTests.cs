namespace QuickOrder.Tests.Application;

using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.UseCases;
using QuickOrder.Core.Domain.ValueObjects;

public class CancelOrderHandlerTests
{
    [Fact]
    public void ExistingOrder_IsCancelled()
    {
        var book = new InMemoryOrderBook();
        new PlaceOrderHandler(book).Handle(new NewOrderCommand("C1", Symbol.PETR4, Side.Buy, 100, 10m));
        var handler = new CancelOrderHandler(book);

        var result = handler.Handle(new CancelOrderCommand("CXL-1", "C1"));

        Assert.True(result.Accepted);
        Assert.Null(result.RejectionReason);
        Assert.Empty(book.GetSnapshot());
    }

    [Fact]
    public void UnknownOrder_IsRejected()
    {
        var book = new InMemoryOrderBook();
        var handler = new CancelOrderHandler(book);

        var result = handler.Handle(new CancelOrderCommand("CXL-1", "DOES-NOT-EXIST"));

        Assert.False(result.Accepted);
        Assert.Equal("ordem nao encontrada", result.RejectionReason);
    }

    [Fact]
    public void CancelTwice_SecondIsRejected()
    {
        var book = new InMemoryOrderBook();
        new PlaceOrderHandler(book).Handle(new NewOrderCommand("C1", Symbol.PETR4, Side.Buy, 100, 10m));
        var handler = new CancelOrderHandler(book);

        var first = handler.Handle(new CancelOrderCommand("CXL-1", "C1"));
        var second = handler.Handle(new CancelOrderCommand("CXL-2", "C1"));

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Equal("ordem nao encontrada", second.RejectionReason);
    }
}
