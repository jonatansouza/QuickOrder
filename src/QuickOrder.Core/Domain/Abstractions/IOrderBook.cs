namespace QuickOrder.Core.Domain.Abstractions;

using QuickOrder.Core.Domain.Entities;

public interface IOrderBook
{
    bool TryAdd(Order order);
    bool TryRemove(string clOrdId);
    IEnumerable<Order> GetSnapshot();
}
