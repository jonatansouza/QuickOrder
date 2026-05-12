using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace QuickOrder.SimulatorWebApi.Controllers;

public record NewOrderPayload(string ClOrdId, string Symbol, string Side, int Qty, decimal Price);
public record CancelOrderPayload(string ClOrdId, string OrigClOrdId, string Symbol, string Side);

[ApiController]
[Route("[controller]")]
public class OrderController : ControllerBase
{
    private readonly FixClientService _fix;
    private readonly IValidator<NewOrderPayload> _newOrderValidator;
    private readonly IValidator<CancelOrderPayload> _cancelValidator;

    public OrderController(
        FixClientService fix,
        IValidator<NewOrderPayload> newOrderValidator,
        IValidator<CancelOrderPayload> cancelValidator)
    {
        _fix = fix;
        _newOrderValidator = newOrderValidator;
        _cancelValidator = cancelValidator;
    }

    /// <summary>
    /// Sends a new order via FIX to the Client, waits for the ExecutionReport.
    /// Side: "BUY" or "SELL"
    /// Symbol: "PETR4" or "VALE3"
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> NewOrder([FromBody] NewOrderPayload payload, CancellationToken ct)
    {
        var validation = await _newOrderValidator.ValidateAsync(payload, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

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

    /// <summary>
    /// Sends an OrderCancelRequest via FIX, waits for ExecutionReport(CANCELED) or OrderCancelReject.
    /// ClOrdId is the unique ID of this cancel request; OrigClOrdId is the order being cancelled.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelOrderPayload payload, CancellationToken ct)
    {
        var validation = await _cancelValidator.ValidateAsync(payload, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        try
        {
            var result = await _fix.SendCancelAsync(
                new CancelOrderRequest(payload.ClOrdId, payload.OrigClOrdId, payload.Symbol, payload.Side), ct);
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
