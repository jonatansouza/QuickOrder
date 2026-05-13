namespace QuickOrder.Core.Domain.Entities;

using QuickOrder.Core.Domain.ValueObjects;
using System.Diagnostics;

public class Order
{
    public string ClOrdId { get; }
    public Symbol Symbol { get; }
    public Side Side { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public long AcceptedAtTicks { get; }

    private Order(string clOrdId, Symbol symbol, Side side, int quantity, decimal price, long acceptedAtTicks)
    {
        ClOrdId = clOrdId;
        Symbol = symbol;
        Side = side;
        Quantity = quantity;
        Price = price;
        AcceptedAtTicks = acceptedAtTicks;
    }

    public static bool TryCreate(string clOrdId, Symbol symbol, Side side, int quantity, decimal price, out Order? order, out string? errorReason)
    {
        order = null;

        if (string.IsNullOrEmpty(clOrdId))
        {
            errorReason = "ClOrdId obrigatorio"; return false;
        }
        if (quantity <= 0 || quantity >= 100_000)
        {
            errorReason = "Quantidade invalida"; return false;
        }
        if (price <= 0 || price >= 1000 || decimal.Round(price, 2) != price)
        {
            errorReason = "Preco invalido"; return false;
        }

        order = new Order(clOrdId, symbol, side, quantity, price, Stopwatch.GetTimestamp());
        errorReason = null;
        return true;
    }
}
