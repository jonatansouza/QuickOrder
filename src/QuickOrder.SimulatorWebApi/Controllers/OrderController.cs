using Microsoft.AspNetCore.Mvc;

namespace QuickOrder.SimulatorWebApi.Controllers;

public record NewOrderPayload(string ClOrdId, string Symbol, string Side, int Qty, decimal Price);

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly FixClientService _fix;

    public OrderController(FixClientService fix) => _fix = fix;

    /// <summary>
    /// Sends a new order via FIX to the Client, waits for the ExecutionReport.
    /// Side: "BUY" or "SELL"
    /// Symbol: "PETR4" or "VALE3"
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> NewOrder([FromBody] NewOrderPayload payload, CancellationToken ct)
    {
        try
        {
            var result = await _fix.SendOrderAsync(
                new OrderRequest(payload.ClOrdId, payload.Symbol, payload.Side, payload.Qty, payload.Price), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { error = ex.Message });
        }
    }
}
