namespace QuickOrder.Core.Application.Results;

public record CancelOrderResult(bool Accepted, string? RejectionReason = null)
{
    public static CancelOrderResult Ok() => new(true);
    public static CancelOrderResult Reject(string reason) => new(false, reason);
}
