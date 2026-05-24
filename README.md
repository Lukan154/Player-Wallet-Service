# Player Wallet Service

Aspire-orchestrated player wallet API (Orleans grains, PostgreSQL persistence, Kafka events).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Postgres and Kafka run in containers via Aspire — you do **not** need separate installs)
- [Aspire CLI](https://aspire.dev/reference/cli/overview/) (optional)

## Run locally

```powershell
cd "Player Wallet Service.AppHost"
dotnet run
```

Or:

```powershell
aspire run --project "Player Wallet Service.AppHost"
```

On first startup, the API applies Orleans PostgreSQL scripts to `walletdb` automatically if tables are missing.

Open the **Aspire dashboard** (typically `https://localhost:17082`), then use the **apiservice** HTTPS URL for API calls (often `https://localhost:7373` — use the port shown in the dashboard, not the dashboard port itself).

## API examples

Replace `{port}` with the **apiservice** port from the dashboard. Use any GUID for `{playerId}`.

**Check balance**

```http
GET https://localhost:{port}/players/3fa85f64-5717-4562-b3fc-2c963f66afa6/balance
```

**Add funds**

```http
POST https://localhost:{port}/players/3fa85f64-5717-4562-b3fc-2c963f66afa6/funds
Content-Type: application/json

{ "amount": 100.00 }
```

**Deduct funds**

```http
POST https://localhost:{port}/players/3fa85f64-5717-4562-b3fc-2c963f66afa6/funds/deduct
Content-Type: application/json

{ "amount": 25.00 }
```

**Insufficient funds** → `409 Conflict` with error details; balance unchanged.

**Invalid amount** (zero or negative) → `400` validation problem.

**Malformed JSON body** → `400` with problem details (not `500`).

### PowerShell (recommended)

Use **`Invoke-RestMethod`** for POST bodies. On Windows, `curl.exe -d '{"amount":100}'` often strips JSON quotes and causes a **500/400** even when the API is healthy.

```powershell
$base = "https://localhost:7373"   # apiservice port from Aspire dashboard
$id = [guid]::NewGuid()

Invoke-RestMethod "$base/players/$id/balance"

Invoke-RestMethod "$base/players/$id/funds" -Method Post -Body '{"amount":100}' -ContentType "application/json"

Invoke-RestMethod "$base/players/$id/funds/deduct" -Method Post -Body '{"amount":25}' -ContentType "application/json"

Invoke-RestMethod "$base/players/$id/balance"
```

If you see certificate errors on older PowerShell, use the dashboard’s HTTP endpoint if exposed, or `curl.exe -k` for GET only.

**curl with a JSON file** (alternative on Windows):

```powershell
Set-Content -Path "$env:TEMP\body.json" -Value '{"amount":100}' -NoNewline -Encoding utf8
curl.exe -k -X POST "$base/players/$id/funds" -H "Content-Type: application/json" -d "@$env:TEMP\body.json"
```

### Persistence check

After add/deduct, restart AppHost and call `GET .../balance` for the same `playerId` — the balance should match. Data is stored in PostgreSQL table **`OrleansStorage`** inside the Aspire Docker Postgres (`walletdb`).

To inspect rows without installing Postgres locally:

```powershell
docker ps   # find postgres container name
docker exec -it <postgres-container> psql -U postgres -d walletdb -c "SELECT graintypestring, version, modifiedon FROM orleansstorage;"
```

(Password is shown on the postgres resource in the Aspire dashboard.)

## Kafka wallet events (Phase 3)

Successful add/deduct operations and rejected deductions publish JSON events to the **`wallet-events`** topic.

| Event | When |
|-------|------|
| `FundsAdded` | Add funds succeeded |
| `FundsDeducted` | Deduct succeeded |
| `DeductionRejected` | Deduct failed — insufficient funds (HTTP 409) |

Example payload:

```json
{
  "eventType": "FundsAdded",
  "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 100,
  "balance": 100,
  "occurredAt": "2026-05-23T12:00:00+00:00"
}
```

Check ApiService logs for `Published FundsAdded...` after a wallet operation. Topic name is configurable via `WalletKafka:TopicName` in `appsettings.json`.

## Orleans database scripts (manual)

Scripts run automatically on startup. To apply manually (e.g. troubleshooting):

1. Connect to `walletdb` (connection string from Aspire dashboard / user secrets).
2. Run in order:
   - `Player Wallet Service.ApiService/scripts/orleans/PostgreSQL-Main.sql`
   - `Player Wallet Service.ApiService/scripts/orleans/PostgreSQL-Persistence.sql`

Upstream source: [dotnet/orleans AdoNet scripts](https://github.com/dotnet/orleans/tree/main/src/AdoNet).

## Projects

| Project | Role |
|---------|------|
| `Player Wallet Service.AppHost` | Postgres (`walletdb`), Kafka; starts ApiService |
| `Player Wallet Service.ApiService` | HTTP API + co-hosted Orleans silo; grain state in PostgreSQL |
| `Player Wallet Service.ServiceDefaults` | Telemetry, health checks, service discovery |
| `Player Wallet Service.Tests` | Aspire integration / component tests (xUnit) |
| `Player Wallet Service.Benchmarks` | NBomber load tests (balance, add, deduct) |

## Component tests (Phase 4)

Requires **Docker Desktop** (tests start Postgres, Kafka, and ApiService via AppHost).

```powershell
dotnet test "Player Wallet Service.Tests\Player Wallet Service.Tests.csproj"
```

Tests cover wallet HTTP flows, validation, insufficient funds, and Kafka `FundsAdded` publishing.

## Load benchmarks (Phase 5)

Requires **AppHost running** separately (Docker + ApiService). Uses [NBomber](https://nbomber.com/) to load-test the three wallet endpoints.

**Quick smoke** (~30 seconds total):

```powershell
dotnet run --project "Player Wallet Service.Benchmarks" -- --smoke
```

**Full profile** (1000 RPS × 5 minutes × 3 scenarios ≈ 21 minutes with 2-minute cooldowns):

```powershell
dotnet run --project "Player Wallet Service.Benchmarks" -- --rate 1000 --minutes 5
```

**Write paths at lower rate** (recommended locally — avoids Windows socket exhaustion):

```powershell
dotnet run --project "Player Wallet Service.Benchmarks" -- --scenario add --rate 200 --minutes 5
dotnet run --project "Player Wallet Service.Benchmarks" -- --scenario deduct --rate 200 --minutes 5
```

**Options:**

| Flag | Default | Meaning |
|------|---------|---------|
| `--base-url` | `http://localhost:5403` | ApiService URL (use HTTP or `-k` curl URL from dashboard) |
| `--rate` | `1000` | Target requests per second |
| `--minutes` | `5` | Duration per scenario |
| `--scenario` | `all` | `balance`, `add`, `deduct`, or `all` |
| `--pool-size` | `5000` | Player IDs reused per scenario (warm grains) |
| `--cooldown-seconds` | `120` | Pause between scenarios (`0` with `--smoke`) |
| `--no-cooldown` | off | Skip pause between scenarios |
| `--smoke` | off | ~10 RPS for ~9 seconds per scenario |

Reports are saved under `reports/getbalance/`, `reports/addfunds/`, `reports/deductfunds/` (HTML + CSV).

### Observed results

**`get_balance` — 1000 RPS × 5 min** (2026-05-24, player pool reuse):

| Metric | Result |
|--------|--------|
| Requests | 300,000 |
| Success | **100%** |
| Achieved RPS | **1000.0** |
| p50 / p95 / p99 | **0.71 ms / 1.44 ms / 3.86 ms** |

Report: `reports/getbalance/nbomber_report_2026-05-24--15-15-37.csv`

**`add_funds` — 200 RPS × 5 min** (2026-05-24, player pool reuse):

| Metric | Result |
|--------|--------|
| Requests | 60,000 |
| Success | **100%** |
| Achieved RPS | **200.0** |
| p50 / p95 / p99 | **16.9 ms / 18.6 ms / 26.9 ms** |

Report: `reports/addfunds/nbomber_report_2026-05-24--15-27-39.csv`

**`deduct_funds` — 200 RPS × 5 min** (2026-05-24, player pool reuse, pre-seeded wallets):

| Metric | Result |
|--------|--------|
| Requests | 60,000 |
| Success | **100%** |
| Achieved RPS | **200.0** |
| p50 / p95 / p99 | **16.9 ms / 18.1 ms / 22.5 ms** |

Report: `reports/deductfunds/nbomber_report_2026-05-24--15-34-25.csv`

**Summary:** read path meets 1k RPS × 5 min; write paths validated at 200 RPS × 5 min (1000 RPS write not sustained locally). See [ENGINEERING_JOURNAL.md](./ENGINEERING_JOURNAL.md) for full analysis.

## Phase 2 notes

- Wallet balances are stored in **PostgreSQL** via Orleans ADO.NET grain persistence (`OrleansStorage` table).
- **Redis** and **Aspire Orleans hosting** were removed after Phase 1; clustering is in-process (`UseLocalhostClustering`).

## Phase 3 notes

- Wallet **events** are published to Kafka topic **`wallet-events`** (`FundsAdded`, `FundsDeducted`, `DeductionRejected`).
- Events are published from HTTP endpoints after grain state is saved; Kafka failures are logged but do not roll back the wallet operation.

## Phase 4 notes

- **Component tests** in `Player Wallet Service.Tests` spin up the full AppHost and assert HTTP + Kafka behavior.

## Phase 5 notes

- **Load benchmarks** in `Player Wallet Service.Benchmarks` (NBomber). All scenarios reuse a player pool (default 5,000 IDs); deduct pre-seeds balances.
- **`get_balance`:** **1000 RPS × 5 min**, 100% success, p95 ~1.4 ms — [`reports/getbalance/nbomber_report_2026-05-24--15-15-37.csv`](reports/getbalance/nbomber_report_2026-05-24--15-15-37.csv)
- **`add_funds`:** **200 RPS × 5 min**, 100% success, p95 ~18.6 ms — [`reports/addfunds/nbomber_report_2026-05-24--15-27-39.csv`](reports/addfunds/nbomber_report_2026-05-24--15-27-39.csv)
- **`deduct_funds`:** **200 RPS × 5 min**, 100% success, p95 ~18.1 ms — [`reports/deductfunds/nbomber_report_2026-05-24--15-34-25.csv`](reports/deductfunds/nbomber_report_2026-05-24--15-34-25.csv)
- Full **1k RPS write** target not sustained on local Windows + Docker; read path meets challenge target.

See [ENGINEERING_JOURNAL.md](./ENGINEERING_JOURNAL.md) for design decisions and a log of Phase 2 mistakes / fixes.
