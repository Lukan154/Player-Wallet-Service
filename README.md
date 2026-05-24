# Player Wallet Service

Aspire-orchestrated wallet API: Orleans grains, PostgreSQL persistence, Kafka events.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Postgres and Kafka run in containers — no separate installs)

## Run

```powershell
dotnet run --project "Player Wallet Service.AppHost"
```

Open the **Aspire dashboard** (e.g. `https://localhost:17082`) and note the **apiservice** URL — that is the wallet API, not the dashboard port.

Orleans PostgreSQL scripts apply automatically on first startup.

## API

Use any GUID as `{playerId}`. Replace `{port}` with the apiservice port from the dashboard.

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/players/{playerId}/balance` | Current balance |
| POST | `/players/{playerId}/funds` | Add funds — body: `{ "amount": 100 }` |
| POST | `/players/{playerId}/funds/deduct` | Deduct funds — body: `{ "amount": 25 }` |

**Responses:** insufficient funds → `409`; invalid amount or malformed JSON → `400`.

### PowerShell example

Prefer **`Invoke-RestMethod`** for POST bodies. On Windows, `curl.exe -d '{"amount":100}'` often breaks JSON quoting.

```powershell
$base = "https://localhost:7373"   # apiservice port from dashboard
$id = [guid]::NewGuid()

Invoke-RestMethod "$base/players/$id/balance"
Invoke-RestMethod "$base/players/$id/funds" -Method Post -Body '{"amount":100}' -ContentType "application/json"
Invoke-RestMethod "$base/players/$id/funds/deduct" -Method Post -Body '{"amount":25}' -ContentType "application/json"
Invoke-RestMethod "$base/players/$id/balance"
```

Balances persist in PostgreSQL (`OrleansStorage` table). Restart AppHost and re-query the same `playerId` to verify.

Wallet operations also publish events to Kafka topic **`wallet-events`**.

## Tests

Requires Docker Desktop.

```powershell
dotnet test "Player Wallet Service.Tests\Player Wallet Service.Tests.csproj"
```

## Benchmarks

Start AppHost first, then:

```powershell
dotnet run --project "Player Wallet Service.Benchmarks" -- --smoke
```

See [ENGINEERING_JOURNAL.md](./ENGINEERING_JOURNAL.md) for options, results, and design decisions.

## Projects

| Project | Role |
|---------|------|
| `Player Wallet Service.AppHost` | Postgres, Kafka, ApiService |
| `Player Wallet Service.ApiService` | HTTP API + Orleans silo |
| `Player Wallet Service.Tests` | Integration tests |
| `Player Wallet Service.Benchmarks` | NBomber load tests |
