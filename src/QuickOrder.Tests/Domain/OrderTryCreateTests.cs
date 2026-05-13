namespace QuickOrder.Tests.Domain;

using QuickOrder.Core.Domain.Entities;
using QuickOrder.Core.Domain.ValueObjects;

public class OrderTryCreateTests
{
    [Fact]
    public void ValidOrder_IsCreated()
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, 100, 10.50m, out var order, out var reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.NotNull(order);
        Assert.Equal("C1", order!.ClOrdId);
        Assert.Equal(Symbol.PETR4, order.Symbol);
        Assert.Equal(Side.Buy, order.Side);
        Assert.Equal(100, order.Quantity);
        Assert.Equal(10.50m, order.Price);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmptyClOrdId_IsRejected(string? clOrdId)
    {
        var ok = Order.TryCreate(clOrdId!, Symbol.PETR4, Side.Buy, 100, 10m, out var order, out var reason);

        Assert.False(ok);
        Assert.Null(order);
        Assert.Equal("ClOrdId obrigatorio", reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_000)]
    [InlineData(100_001)]
    public void QuantityOutOfRange_IsRejected(int qty)
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, qty, 10m, out _, out var reason);

        Assert.False(ok);
        Assert.Equal("Quantidade invalida", reason);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99_999)]
    public void QuantityAtBoundary_IsAccepted(int qty)
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, qty, 10m, out _, out _);

        Assert.True(ok);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(1000)]
    [InlineData(1000.01)]
    public void PriceOutOfRange_IsRejected(decimal price)
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, 100, price, out _, out var reason);

        Assert.False(ok);
        Assert.Equal("Preco invalido", reason);
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(0.999)]
    [InlineData(123.456)]
    public void PriceNotMultipleOfOneCent_IsRejected(decimal price)
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, 100, price, out _, out var reason);

        Assert.False(ok);
        Assert.Equal("Preco invalido", reason);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(10.50)]
    [InlineData(999.99)]
    public void PriceAtBoundary_IsAccepted(decimal price)
    {
        var ok = Order.TryCreate("C1", Symbol.PETR4, Side.Buy, 100, price, out _, out _);

        Assert.True(ok);
    }

    [Fact]
    public void AcceptedAtTicks_IsMonotonicAcrossOrders()
    {
        Order.TryCreate("A", Symbol.PETR4, Side.Buy, 10, 1m, out var first, out _);
        Order.TryCreate("B", Symbol.PETR4, Side.Buy, 10, 1m, out var second, out _);

        Assert.True(second!.AcceptedAtTicks >= first!.AcceptedAtTicks);
    }
}
