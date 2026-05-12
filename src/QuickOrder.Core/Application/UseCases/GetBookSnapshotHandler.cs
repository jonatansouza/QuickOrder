namespace QuickOrder.Core.Application.UseCases;

using QuickOrder.Core.Application.Results;
using QuickOrder.Core.Domain.Abstractions;

public class GetBookSnapshotHandler
{
    private readonly IOrderBook _book;

    public GetBookSnapshotHandler(IOrderBook book) => _book = book;

    public IReadOnlyList<SnapshotGroup> Handle()
    {
        return _book.GetSnapshot()
            .GroupBy(o => new { o.Symbol, o.Side })
            .Select(g => new SnapshotGroup(
                g.Key.Symbol,
                g.Key.Side,
                g.Select(o => new SnapshotOrder(o.Price, o.Quantity)).ToList()
            ))
            .ToList();
    }
}
