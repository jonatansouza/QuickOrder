using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickOrder.Infrastructure.Repositories
{
    using QuickOrder.Core.Domain;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public class OrderRepository
    {
        // Chave: ClOrdId para busca e deleção O(1)
        private readonly ConcurrentDictionary<string, Order> _orders = new(concurrencyLevel: 1, capacity: 100000);

        public bool TryAdd(Order order) => _orders.TryAdd(order.ClOrdId, order);

        public bool TryRemove(string clOrdId) => _orders.TryRemove(clOrdId, out _);

        // Snapshot - Protocolo Livre (Executado fora da thread principal do FIX)
        public IEnumerable<Order> GetSnapshot()
        {
            return _orders.Values
                .OrderBy(o => o.Symbol)
                .ThenBy(o => o.Side)
                .ThenBy(o => o.Price)         // Crescente por preço
                .ThenBy(o => o.AcceptedAtTicks); // Prioridade de tempo (FIFO)
        }
    }
}
