# QuickOrder

Low-latency order management built around two C# services communicating over the FIX 4.2 protocol. Targets an average round-trip below **1 ms** over 100 000 sequential orders.

---

## Architecture

```
┌──────────────────────────────┐
│   External user (FIX)        │   "usuário externo" in the spec
│   QuickOrder.SimulatorWebApi │   REST/Swagger surface for manual orders + benchmark
└──────────────┬───────────────┘
               │ FIX 4.2  ·  TCP :5000  ·  SIMULATOR → CLIENT
               ▼
┌──────────────────────────────┐
│   QuickOrder.Client          │
│   · FIX acceptor   :5000     │
│   · FIX initiator → Server   │
│   · HTTP /book     :7000     │  proxies the global snapshot
│   · HTTP /ledger   :7000     │  local audit of received reports
└──────────────┬───────────────┘
               │ FIX 4.2  ·  TCP :5001  ·  CLIENT → SERVER
               ▼
┌──────────────────────────────┐
│   QuickOrder.Server          │
│   · FIX acceptor   :5001     │
│   · Order book (in-memory)   │
│   · Validation + storage     │
│   · HTTP /book     :7001     │  global snapshot, all clients
└──────────────────────────────┘
```

### Layering

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `QuickOrder.Core/Domain` | `Order`, `Symbol`, `Side`, `IOrderBook` |
| Application | `QuickOrder.Core/Application` | `PlaceOrderHandler`, `CancelOrderHandler`, `GetBookSnapshotHandler` |
| Infrastructure | `QuickOrder.Infrastructure` | FIX adapters, `OrderRepository`, `SnapshotHttpServer`, `OrderLedger` |
| Hosts | `QuickOrder.Server`, `QuickOrder.Client` | Worker + DI composition |
| External | `QuickOrder.SimulatorWebApi` | REST shim wrapping a FIX initiator |
| Tests | `QuickOrder.Tests` | xUnit — domain + use-case coverage |

FIX adapters are thin translators: parse FIX → build command → call handler → build FIX response. No business logic at the adapter layer.

### Domain

| Type | Notes |
|---|---|
| `Symbol` | enum: `PETR4`, `VALE3` |
| `Side` | enum: `Buy`, `Sell` |
| `Order` | immutable; built via `Order.TryCreate` |
| `IOrderBook` | `TryAdd`, `TryRemove`, `GetSnapshot` |

Validation (`Order.TryCreate`):

| Field | Rule |
|---|---|
| `ClOrdId` | non-empty |
| `Symbol` | `PETR4` or `VALE3` |
| `Side` | `Buy` or `Sell` |
| `Quantity` | integer, `> 0` and `< 100 000` |
| `Price` | decimal, `> 0`, `< 1 000`, multiple of `0.01` |

`Symbol`/`Side` are parsed at the FIX boundary by `FixMapping`. Inside the domain only typed enums exist.

### Snapshot (free protocol — HTTP)

| Endpoint | Returns |
|---|---|
| `GET http://localhost:7001/book` | Global order book (Server) |
| `GET http://localhost:7000/book` | Same data, proxied by the Client |
| `GET http://localhost:7000/ledger` | Local audit log of every `ExecutionReport` / `OrderCancelReject` this Client has seen |

Snapshot orders are grouped by `(symbol, side)`. Within each group: ascending `price`, FIFO within the same price. Each item carries `symbol`, `side`, `price`, `quantity`.

---

## How to run

### Docker (production-shaped, 1 CPU / 2 GB per service)

```powershell
docker compose --compatibility up -d --build
```

`--compatibility` translates `deploy.resources.limits` (Swarm-only) to the v2 `cpus:` / `mem_limit:` runtime keys so the limits actually apply to `docker compose up`. Without it they are silently ignored.

Verify limits took effect:

```powershell
docker inspect quickorder-server --format '{{ .HostConfig.NanoCpus }} {{ .HostConfig.Memory }}'
# expected: 1000000000 2147483648   (1 CPU, 2 GiB)
```

Tear down:

```powershell
docker compose down
```

### Local (no Docker)

Three terminals, in order — wait for each `Logon` line before starting the next:

```powershell
# Terminal 1 — Server
dotnet run --project src/QuickOrder.Server -c Release

# Terminal 2 — Client
$env:SNAPSHOT_BIND = "localhost"   # avoid HttpListener admin prompt on Windows
dotnet run --project src/QuickOrder.Client -c Release

# Terminal 3 — Simulator
dotnet run --project src/QuickOrder.SimulatorWebApi -c Release
```

Swagger UI: <http://localhost:5004/swagger>

### Environment variables

| Variable | Default | Used by | Purpose |
|---|---|---|---|
| `SERVER_HOST` | `localhost` | Client | FIX initiator target + snapshot proxy upstream |
| `SERVER_SNAPSHOT_PORT` | `7001` | Client, Server | HTTP snapshot port on the Server |
| `CLIENT_SNAPSHOT_PORT` | `7000` | Client | Local HTTP port for `/book`, `/ledger` |
| `SNAPSHOT_BIND` | `+` | Client, Server | `+` for Docker, `localhost` for Windows-local |
| `CLIENT_HOST` | `localhost` | Simulator | Where to find the Client's FIX acceptor |
| `Logging__LogLevel__QuickOrder` | `Information` | all | set to `Trace` to enable per-message logs |

---

## How to test

### Unit tests

```powershell
dotnet test src/QuickOrder.slnx `
  --logger "trx;LogFileName=tests.trx" `
  --results-directory test-results
```

Coverage:

- `Domain/OrderTryCreateTests` — every validation rule + boundary values
- `Application/PlaceOrderHandlerTests` — accept, reject by domain rule, duplicate `ClOrdId`
- `Application/CancelOrderHandlerTests` — happy path, unknown order, double cancel
- `Application/GetBookSnapshotHandlerTests` — grouping, ascending price, FIFO, cancelled orders excluded

### End-to-end (REST → FIX → REST)

```powershell
# new order
Invoke-RestMethod -Method Post -Uri http://localhost:5004/Order `
  -ContentType "application/json" `
  -Body '{"clOrdId":"E2E-1","symbol":"PETR4","side":"BUY","qty":100,"price":10.50}'

# cancel
Invoke-RestMethod -Method Post -Uri http://localhost:5004/Order/cancel `
  -ContentType "application/json" `
  -Body '{"clOrdId":"CXL-1","origClOrdId":"E2E-1","symbol":"PETR4","side":"BUY"}'

# global snapshot
Invoke-RestMethod http://localhost:7000/book | ConvertTo-Json -Depth 5
```

### Latency benchmark

`BenchmarkController` measures `T2 − T1` per message **inside the Simulator** using `Stopwatch.GetTimestamp()`. The HTTP trigger from your machine to the Simulator is **not** part of the measurement — the whole sequential loop and the timestamps live inside the Simulator process.

```powershell
$out = "test-results/bench-100k.json"
Invoke-RestMethod "http://localhost:5004/Benchmark?count=100000&warmup=1000" -TimeoutSec 600 |
  ConvertTo-Json -Depth 5 | Out-File $out -Encoding utf8
```

| Param | Default | Range | Notes |
|---|---|---|---|
| `count` | `1000` | `1..200 000` | measured iterations |
| `warmup` | `1000` | `0..10 000` | discarded iterations (JIT, session warmup) |
| `symbol` | `PETR4` | `PETR4` or `VALE3` | |
| `side` | `BUY` | `BUY` or `SELL` | |

The response includes `runId`, `count`, `warmup`, `avgMs`, `minMs`, `maxMs`, `p50Ms`, `p95Ms`, `p99Ms`, `p999Ms`. `ClOrdId`s are prefixed with `runId` so consecutive runs do not collide on the order book.

---

## Design notes

- **Adapters are pass-through translators.** Business logic lives in `Application/UseCases/`. The FIX cracker layer parses, calls a handler, returns the result.
- **`Order` is immutable.** Built via `TryCreate`; once accepted, only the book holds it. `AcceptedAtTicks` uses `Stopwatch.GetTimestamp` for monotonic FIFO ordering.
- **`OrderRepository` is a `ConcurrentDictionary`** pre-sized to 100 000 slots — no resizing on the hot path.
- **HTTP uses `HttpListener`**, not Kestrel. Two endpoints don't justify ASP.NET's startup cost in the Server/Client workers.
- **Hot-path logging is at `Trace`.** Per-message events (forward, accept, reject) only render when `Logging__LogLevel__QuickOrder=Trace`. Lifecycle events (`Start`, `Logon`, `Logout`) stay at `Information` so operators still see the service come up.
- **Resource limits are real.** `--compatibility` is required for `docker compose up` to honour `deploy.resources.limits`. The README walks through the verification.

---

## Repository layout

```
QuickOrder/
├── infra/
│   ├── quick-order-client/Dockerfile
│   ├── quick-order-server/Dockerfile
│   └── quick-order.simulation-web-api/Dockerfile
├── src/
│   ├── QuickOrder.Core/                # Domain + Application
│   ├── QuickOrder.Infrastructure/      # FIX adapters, repository, HTTP, ledger
│   ├── QuickOrder.Server/              # Worker host
│   ├── QuickOrder.Client/              # Worker host
│   ├── QuickOrder.SimulatorWebApi/     # REST shim wrapping a FIX initiator
│   ├── QuickOrder.Tests/               # xUnit (domain + use cases)
│   └── QuickOrder.slnx
├── docker-compose.yml
├── README.md
└── ist.txt                              # original challenge spec (pt-BR)
```

---

## Tech

- **.NET 8** / C#
- **QuickFIXn** (`QuickFIXn.Core` + `QuickFIXn.FIX4.2`)
- **xUnit** for tests, **FluentValidation** on the REST boundary
- **Docker Compose** for deployment

