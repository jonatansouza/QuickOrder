namespace QuickOrder.Core.Domain;

// Domain/Order.cs
using System.Diagnostics;

public class Order
{
    public string ClOrdId { get; private set; }
    public string Symbol { get; private set; }
    public char Side { get; private set; }
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
    public long AcceptedAtTicks { get; private set; } // Alta precisão para ordenação FIFO

    private Order() { }

    // Retorna false em vez de Exception para não impactar latência
    public static bool TryCreate(string clOrdId, string symbol, char side, int quantity, decimal price, out Order? order, out string? errorReason)
    {
        order = null;

        if (symbol != "PETR4" && symbol != "VALE3")
        {
            errorReason = "Simbolo invalido"; return false;
        }
        if (side != '1' && side != '2')
        {
            errorReason = "Lado invalido"; return false;
        }
        if (quantity <= 0 || quantity >= 100000)
        {
            errorReason = "Quantidade invalida"; return false;
        }
        if (price <= 0 || price >= 1000 || price % 0.01m != 0)
        {
            errorReason = "Preco invalido"; return false;
        }

        order = new Order
        {
            ClOrdId = clOrdId,
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            AcceptedAtTicks = Stopwatch.GetTimestamp()
        };
        errorReason = null;
        return true;
    }
}
