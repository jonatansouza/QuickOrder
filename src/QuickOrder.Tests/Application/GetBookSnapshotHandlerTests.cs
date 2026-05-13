namespace QuickOrder.Tests.Application;

using QuickOrder.Core.Application.Commands;
using QuickOrder.Core.Application.UseCases;
using QuickOrder.Core.Domain.ValueObjects;

public class GetBookSnapshotHandlerTests
{
    [Fact]
    public void EmptyBook_ReturnsEmpty()
    {
        var book = new InMemoryOrderBook();
        var handler = new GetBookSnapshotHandler(book);

        var snapshot = handler.Handle();

        Assert.Empty(snapshot);
    }

    [Fact]
    public void OrdersAreGroupedBySymbolAndSide()
    {
        var book = new InMemoryOrderBook();
        var place = new PlaceOrderHandler(book);
        place.Handle(new NewOrderCommand("A", Symbol.PETR4, Side.Buy, 10, 10m));
        place.Handle(new NewOrderCommand("B", Symbol.PETR4, Side.Sell, 10, 11m));
        place.Handle(new NewOrderCommand("C", Symbol.VALE3, Side.Buy, 10, 20m));
        place.Handle(new NewOrderCommand("D", Symbol.PETR4, Side.Buy, 5, 9m));

        var snapshot = new GetBookSnapshotHandler(book).Handle();

        Assert.Equal(3, snapshot.Count);
        var petr4Buy = snapshot.Single(g => g.Symbol == Symbol.PETR4 && g.Side == Side.Buy);
        Assert.Equal(2, petr4Buy.Orders.Count);
        Assert.Single(snapshot.Where(g => g.Symbol == Symbol.PETR4 && g.Side == Side.Sell));
        Assert.Single(snapshot.Where(g => g.Symbol == Symbol.VALE3 && g.Side == Side.Buy));
    }

    [Fact]
    public void WithinGroup_OrdersArePriceAscending()
    {
        var book = new InMemoryOrderBook();
        var place = new PlaceOrderHandler(book);
        place.Handle(new NewOrderCommand("HIGH", Symbol.PETR4, Side.Buy, 10, 30m));
        place.Handle(new NewOrderCommand("LOW", Symbol.PETR4, Side.Buy, 10, 10m));
        place.Handle(new NewOrderCommand("MID", Symbol.PETR4, Side.Buy, 10, 20m));

        var group = new GetBookSnapshotHandler(book).Handle().Single();

        Assert.Equal(new[] { 10m, 20m, 30m }, group.Orders.Select(o => o.Price));
    }

    [Fact]
    public void SamePrice_FollowsFifo()
    {
        var book = new InMemoryOrderBook();
        var place = new PlaceOrderHandler(book);
        place.Handle(new NewOrderCommand("FIRST", Symbol.PETR4, Side.Buy, 10, 10m));
        place.Handle(new NewOrderCommand("SECOND", Symbol.PETR4, Side.Buy, 20, 10m));
        place.Handle(new NewOrderCommand("THIRD", Symbol.PETR4, Side.Buy, 30, 10m));

        var group = new GetBookSnapshotHandler(book).Handle().Single();

        Assert.Equal(new[] { 10, 20, 30 }, group.Orders.Select(o => o.Quantity));
    }

    [Fact]
    public void CancelledOrders_AreNotInSnapshot()
    {
        var book = new InMemoryOrderBook();
        new PlaceOrderHandler(book).Handle(new NewOrderCommand("A", Symbol.PETR4, Side.Buy, 10, 10m));
        new PlaceOrderHandler(book).Handle(new NewOrderCommand("B", Symbol.PETR4, Side.Buy, 20, 11m));
        new CancelOrderHandler(book).Handle(new CancelOrderCommand("CXL-1", "A"));

        var group = new GetBookSnapshotHandler(book).Handle().Single();

        Assert.Single(group.Orders);
        Assert.Equal(11m, group.Orders[0].Price);
    }
}
