using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace QuickOrder.SimulatorWebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly FixClientService _fix;

    public BenchmarkController(FixClientService fix) => _fix = fix;

    /// <summary>
    /// Sends N orders sequentially and measures T2-T1 (FIX round-trip) per order.
    /// Returns avg, min, max, p50, p95, p99, p999 in milliseconds.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Run(
        [FromQuery] int count = 1000,
        [FromQuery] string symbol = "PETR4",
        [FromQuery] string side = "BUY",
        CancellationToken ct = default)
    {
        if (count < 1 || count > 200_000)
            return BadRequest(new { error = "count must be between 1 and 200000" });

        var freq = (double)Stopwatch.Frequency;
        var latencies = new double[count];

        for (var i = 0; i < count; i++)
        {
            var clOrdId = $"BENCH-{i:D7}";
            var t1 = Stopwatch.GetTimestamp();

            try
            {
                await _fix.SendOrderAsync(new OrderRequest(clOrdId, symbol, side, 100, 10.50m), ct);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed at order {i}: {ex.Message}" });
            }

            latencies[i] = (Stopwatch.GetTimestamp() - t1) / freq * 1000.0;
        }

        Array.Sort(latencies);

        int Idx(double pct) => Math.Min((int)(count * pct), count - 1);

        return Ok(new
        {
            count,
            avgMs   = Math.Round(latencies.Average(), 3),
            minMs   = Math.Round(latencies[0], 3),
            maxMs   = Math.Round(latencies[count - 1], 3),
            p50Ms   = Math.Round(latencies[Idx(0.50)], 3),
            p95Ms   = Math.Round(latencies[Idx(0.95)], 3),
            p99Ms   = Math.Round(latencies[Idx(0.99)], 3),
            p999Ms  = Math.Round(latencies[Idx(0.999)], 3),
        });
    }
}
