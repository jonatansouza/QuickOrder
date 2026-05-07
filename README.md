# QuickOrder

A low-latency order management system with two dockerized services communicating via the FIX protocol.

## Technologies

- **Language:** C# / .NET 8
- **FIX Protocol:** [QuickFIXn](https://quickfixn.org/) (`QuickFIXn.Core` + `QuickFIXn.FIX4.2`)
- **Messaging:** FIX 4.2 (orders and cancellations)
- **Snapshot API:** HTTP/REST (free protocol)
- **Containerization:** Docker + Docker Compose

## Architecture

```
External User
    │
    │  FIX (port 5000)
    ▼
┌─────────────────────┐
│       Client        │  FIX initiator → Server
│  - FIX acceptor     │  FIX (port 5001)
│  - FIX initiator    │ ─────────────────────────► ┌─────────────────────┐
│  - HTTP snapshot    │                             │       Server        │
└─────────────────────┘ ◄───────────────────────── │  - FIX acceptor     │
    │                    ExecutionReport            │  - Order book       │
    │  HTTP (port 7000)                             │  - HTTP snapshot    │
    ▼                                               └─────────────────────┘
External User
```

### Project Structure

```
QuickOrder/
  src/
    QuickOrder.Core/            # Domain: models, order book, validation
    QuickOrder.Infrastructure/  # QuickFIXn session wrappers (FixAcceptor, FixInitiator, QMessage)
    QuickOrder.Client/          # Worker: FIX acceptor + FIX initiator + HTTP snapshot relay
    QuickOrder.Server/          # Worker: FIX acceptor + order book + HTTP snapshot
  infra/
    quick-order-client/Dockerfile
    quick-order-server/Dockerfile
  docker-compose.yml
```

### Domain

| Type | Description |
|------|-------------|
| `Symbol` | `PETR4` or `VALE3` |
| `Side` | `Buy` or `Sell` |
| `Order` | `ClOrdId`, `Symbol`, `Side`, `Quantity`, `Price`, `AcceptedAt` |
| `OrderValidator` | Validates all fields per spec |
| `OrderBook` | Thread-safe in-memory store; snapshot sorted by Symbol → Side → Price → time |

### Validation Rules

| Field | Rule |
|-------|------|
| Symbol | Must be `PETR4` or `VALE3` |
| Side | Must be `Buy` or `Sell` |
| Quantity | Positive integer, less than 100,000 |
| Price | Positive decimal, multiple of `0.01`, less than `1,000` |

## How to Run

### With Docker Compose (recommended)

```bash
docker compose up --build
```

Both services start automatically. The Client connects to the Server via FIX after the Server is ready.

Resource limits applied per container: **1 CPU / 2 GB RAM**.

### Locally (development)

**Terminal 1 — Server:**
```bash
cd src
dotnet run --project QuickOrder.Server
```

**Terminal 2 — Client:**
```bash
cd src
dotnet run --project QuickOrder.Client
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `SERVER_HOST` | `localhost` | Server hostname (use `server` in Docker) |
| `SERVER_FIX_PORT` | `5001` | Server FIX acceptor port |

## Latency Target

Average round-trip `T2 - T1 < 1ms` over 100,000 sequential order requests, where:
- `T1` = timestamp when external user sends the request
- `T2` = timestamp when external user receives the response

---

> This is a challenge by [Coodesh](https://coodesh.com/)
