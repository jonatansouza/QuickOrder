namespace QuickOrder.Core.Application.Results;

public record PlaceOrderResult(bool Accepted, string? RejectionReason = null)
{
    public static PlaceOrderResult Ok() => new(true);
    public static PlaceOrderResult Reject(string reason) => new(false, reason);
}
