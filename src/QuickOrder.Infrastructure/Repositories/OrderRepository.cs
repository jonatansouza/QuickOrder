namespace QuickOrder.Infrastructure.Repositories;

using QuickOrder.Core.Domain.Abstractions;
using QuickOrder.Core.Domain.Entities;
using System.Collections.Concurrent;

public class OrderRepository : IOrderBook
{
    private readonly ConcurrentDictionary<string, Order> _orders = new(concurrencyLevel: 1, capacity: 100_000);

    public bool TryAdd(Order order) => _orders.TryAdd(order.ClOrdId, order);

    public bool TryRemove(string clOrdId) => _orders.TryRemove(clOrdId, out _);

    public IEnumerable<Order> GetSnapshot()
    {
        return _orders.Values
            .OrderBy(o => o.Symbol)
            .ThenBy(o => o.Side)
            .ThenBy(o => o.Price)
            .ThenBy(o => o.AcceptedAtTicks);
    }
}
