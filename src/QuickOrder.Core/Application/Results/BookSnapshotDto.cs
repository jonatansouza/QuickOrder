namespace QuickOrder.Core.Application.Results;

using QuickOrder.Core.Domain.ValueObjects;

public record SnapshotOrder(decimal Price, int Quantity);

public record SnapshotGroup(Symbol Symbol, Side Side, IReadOnlyList<SnapshotOrder> Orders);
