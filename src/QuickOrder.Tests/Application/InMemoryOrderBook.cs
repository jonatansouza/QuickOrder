namespace QuickOrder.Tests.Application;

using QuickOrder.Core.Domain.Abstractions;
using QuickOrder.Core.Domain.Entities;

internal class InMemoryOrderBook : IOrderBook
{
    private readonly Dictionary<string, Order> _orders = new();

    public bool TryAdd(Order order) => _orders.TryAdd(order.ClOrdId, order);

    public bool TryRemove(string clOrdId) => _orders.Remove(clOrdId);

    public IEnumerable<Order> GetSnapshot()
        => _orders.Values
            .OrderBy(o => o.Symbol)
            .ThenBy(o => o.Side)
            .ThenBy(o => o.Price)
            .ThenBy(o => o.AcceptedAtTicks);
}
