# Engineering Journal — Player Wallet Service

---

## 1. Approach & Tooling

### Tools used

| Tool | How it was used |
|------|-----------------|
| **Cursor (Agent mode)** | Primary tool for the entire challenge. Multi-file edits, project scaffolding, running builds/tests, and documentation updates. |
| **Chat-based prompting** | Planning, explanations, debugging, and phase-by-phase direction. |
| **Agentic coding** | Agent implemented features autonomously across AppHost, ApiService, tests, and benchmarks — not just inline suggestions. |

No separate inline Copilot or Claude Code session was used; work stayed in one Cursor Agent conversation tied to this repo.

### Workflow

1. **Plan first** — Linked the Elantil challenge PDF and asked for an architecture + phased plan. Agent output five phases: Foundation → Wallet Domain → Kafka → Component Tests → Benchmarks.
2. **Implement in phases** — Each phase was a explicit user prompt. Agent built; user ran AppHost and reported results.
3. **Debug by pasting errors** — Build errors, dashboard status, HTTP responses, and log snippets were pasted back; agent diagnosed and fixed.
4. **Validate manually** — `Invoke-RestMethod` / `curl`, Aspire dashboard, `dotnet test`, NBomber runs.

### Why this mix

- **Agent mode** suited greenfield scaffolding (Orleans grain, Aspire AppHost, Kafka wiring, test project, NBomber project) where many files change at once.
- **Chat prompting** suited learning and decisions.
- **Phased delivery** matched the 4–6 hour budget and kept each review surface small — easier to catch mistakes like duplicate Orleans config or wrong API port.
- **User-driven testing** caught issues the agent missed (PowerShell `curl` JSON mangling, dashboard port vs apiservice port).

---

## 2. Key Prompts & Iterations

### Prompt 1 — Worked well on first pass: initial plan

**Prompt:**
> `Elantil_PlayerWallet_AI_Assisted_Challenge.pdf` — I have this task I need to do. Let's create a plan for it.

**Why it worked:**
- Attached the **source requirements** (PDF) instead of paraphrasing.
- Single clear ask: **plan**, not "build everything now".
- Agent returned deliverables checklist, recommended architecture (Orleans + Postgres + Kafka), project layout, and a phased timeline aligned to the challenge.

**Outcome:** Used as the roadmap for all five phases. No major rework of the plan was needed.

---

### Prompt 2 — AI got it wrong: Orleans 9 `UseJsonFormat`

**Prompt (user pasted build error):**
> `'AdoNetGrainStorageOptions' does not contain a definition for 'UseJsonFormat'`

**What was wrong:** Agent used `options.UseJsonFormat = true` in `AddAdoNetGrainStorage` — an API removed in Orleans 9. Likely copied from older Orleans docs/examples.

**Diagnosis:** Exact compiler error pointed at line 30 in `Program.cs`. Orleans 9 uses default JSON grain storage serializers; no `UseJsonFormat` flag.

**Fix:** Removed `UseJsonFormat`. Kept `PlayerWalletGrainState` as a plain POCO compatible with default ADO.NET JSON storage.

---

### Prompt 3 — AI got it wrong: POST `/funds` returned 500 (looked like persistence bug)

**Prompt (user pasted):**
> `curl.exe -k -X POST ".../funds" -H "Content-Type: application/json" -d '{"amount":100}'` → **500 Internal Server Error**  
> (GET balance worked; user asked if data was being saved)

**What was wrong:** Initially easy to assume grain persistence or Orleans misconfiguration. Root cause was **client-side**: PowerShell + `curl.exe -d '{"amount":100}'` often sends **invalid JSON** (`{amount:100}`). Server logged `JsonException: 'a' is an invalid start of a property name`.

**Diagnosis:**
1. GET worked → API and grain activation OK.
2. Only POST failed → binding/validation layer.
3. Server logs showed `BadHttpRequestException` / JSON parse errors, not Postgres errors.
4. Confirmed with `Invoke-RestMethod` and proper JSON body → POST succeeded; balance persisted in `OrleansStorage`.

**Fix:**
- Documented **`Invoke-RestMethod`** as the reliable Windows client in README.
- Added **`BadHttpRequestExceptionHandler`** so malformed JSON returns **400** with ProblemDetails instead of opaque **500**.

---

### Prompt 4 — Iteration: benchmark failures → design improvements

**Prompt:**
> Here are the reports: [NBomber logs with `SocketException (10055)`, deduct stopped at 5835 fails]

**What was wrong:** First full benchmark run at 1000 RPS used **new GUID per request** for balance/add; write scenarios exhausted Windows ephemeral ports when client and server shared localhost.

**Diagnosis:** Parsed CSV reports and logs — `get_balance` succeeded; `add_funds` degraded at ~1m43s; `deduct_funds` failed immediately after (OS still exhausted).

**Fix (agent + user re-runs):**
- Reuse **5,000-player pool** across all scenarios.
- **2-minute cooldown** between scenarios.
- Run writes separately at **200 RPS** — all three endpoints then validated at 100% success (see §5 Benchmark Results).

---

## 3. Architectural Decisions

For each choice: **decision**, **alternatives**, **rationale**, and **who proposed it**.

### 3.1 Co-hosted Orleans silo in ApiService

| | |
|---|---|
| **Decision** | Single `ApiService` project runs both the ASP.NET HTTP host and the Orleans silo (`UseOrleans` in `Program.cs`). |
| **Alternatives** | Separate Silo + API client projects; Aspire `AddOrleans` hosting package on AppHost. |
| **Why** | Challenge asks for one wallet microservice. Co-hosting reduces AppHost complexity and avoids extra inter-process grain calls. **User** chose to keep one service; **AI** recommended co-hosting in the initial plan. |
| **Trade-off** | HTTP and grain work share one process — acceptable for this challenge scale. |

### 3.2 Grain persistence: Redis (Phase 1) → PostgreSQL ADO.NET (Phase 2+)

| | |
|---|---|
| **Decision** | Phase 1: Redis grain storage for fast Aspire/Orleans setup. Phase 2+: **Postgres `walletdb`** via `AddAdoNetGrainStorage` + `OrleansDatabaseMigrator` on startup. |
| **Alternatives** | Postgres from day one; Azure Storage; keep Redis permanently. |
| **Why** | Redis had declarative Aspire Orleans integration — fastest path to a working demo (**AI** suggestion for Phase 1). Challenge requires **database persistence** — **user** wanted Postgres; migrated in Phase 2. `OrleansStorage` table holds JSON grain state. |
| **Trade-off** | Two-step migration added brief complexity; aligned with "persist to a database" requirement. |

### 3.3 Grain state structure

| | |
|---|---|
| **Decision** | `PlayerWalletGrainState` — plain POCO: `PlayerId` (Guid), `Balance` (decimal). No `[GenerateSerializer]` / `[Id]` attributes. One `PlayerWalletGrain` per player (grain key = player id). |
| **Alternatives** | Orleans codegen serializers (`[GenerateSerializer]`); separate DB table for balances outside Orleans; storing only `Balance` without `PlayerId` in state. |
| **Why** | Orleans 9 ADO.NET default storage uses **JSON** (Newtonsoft), not codegen binary serializers. Attempts to mix `[GenerateSerializer]` with ADO.NET caused startup/persistence errors (**learned via debugging**). Including `PlayerId` in state makes stored JSON self-describing. **AI** implemented; **user** validation caught persistence issues that led to simplifying the POCO. |
| **Operations** | `AddFunds` / `DeductFunds` validate amount > 0; deduct throws `InsufficientFundsException` if balance too low; every mutation calls `WriteStateAsync()`. |

### 3.4 Event schema & Kafka publishing

| | |
|---|---|
| **Decision** | Topic: **`wallet-events`**. JSON payload: `eventType`, `playerId`, `amount`, `balance`, `occurredAt`. Types: `FundsAdded`, `FundsDeducted`, `DeductionRejected`. Kafka **key** = `playerId` (ordering per player). Publish from **HTTP endpoints after** grain save — not inside the grain. |
| **Alternatives** | Publish from grain (couples Orleans to Kafka); Avro/Protobuf schemas; separate topics per event type; fail HTTP request if Kafka down. |
| **Why** | **AI** proposed endpoint-level publishing to keep persistence and messaging separate. Postgres remains source of truth; Kafka failures are **logged but do not roll back** the wallet operation (best-effort notifications). `DeductionRejected` published on 409 so downstream systems see failed deduct attempts. **User** asked what Kafka is for — clarified it's for downstream consumers (analytics, anti-fraud), not logging alone. |
| **No `WaitFor(kafka)`** | Learned from Phase 2 timeouts; ApiService waits for Postgres only. |

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

### 3.5 Error handling approach

| | |
|---|---|
| **Decision** | ASP.NET **ProblemDetails** for API errors. Domain: `InsufficientFundsException` → **409 Conflict**. Validation: zero/negative amount, null body → **400**. **`BadHttpRequestExceptionHandler`** for malformed JSON → **400** (not 500). |
| **Alternatives** | Custom error DTOs; generic 500 for all failures; return 200 with error envelope. |
| **Why** | ProblemDetails is standard for minimal APIs. **409** clearly signals business rule (insufficient funds) vs **400** (bad input). Custom handler added after **user** hit opaque 500s from bad `curl` JSON — real bug was client-side, but server response was improved anyway. |

### 3.6 Component testing (Phase 4)

| | |
|---|---|
| **Decision** | `Player Wallet Service.Tests` with **`Aspire.Hosting.Testing`** — spins up full AppHost (Postgres + Kafka + ApiService in Docker). Shared fixture; 9 tests for HTTP flows, validation, 409, and Kafka `FundsAdded`. |
| **Alternatives** | Unit tests with mocked `IClusterClient`; Testcontainers without Aspire; manual testing only. |
| **Why** | Challenge asks for component tests. Aspire testing factory matches how the app actually runs (**AI** implemented). Caught Kafka consumer timing issue (needed poll loop for partition assignment). |

### 3.7 Load benchmark design (Phase 5)

| | |
|---|---|
| **Decision** | NBomber 6 console app; scenarios run against **already-running AppHost**; default HTTP `localhost:5403`; **5,000-player pool reuse**; deduct pre-seeds balances; **2-min cooldown** between scenarios when running `all`. |
| **Alternatives** | k6; starting AppHost from NBomber; new GUID every request; HTTPS only. |
| **Why** | NBomber fits .NET stack. External AppHost matches ops workflow. Pool reuse measures **warm-grain** steady state vs worst-case grain creation (**AI**, refined after **user** benchmark failures). Separate write runs at 200 RPS after 1000 RPS socket exhaustion on Windows localhost. |

---

## 4. Implementation Log

### Phase 1 — Foundation

1. Solution from Aspire template; removed `Player Wallet Service.Web` (not required).
2. AppHost orchestrates Postgres (`walletdb`), later Kafka.
3. ApiService: co-hosted Orleans silo, `PlayerWalletGrain` returning balance `0` for new players.
4. Phase 1 grain storage: **Redis** (quick demo); Postgres container provisioned but unused for grains.
5. Orleans OpenTelemetry in ServiceDefaults (metrics + traces in Aspire dashboard).

**Verified:** `GET /players/{id}/balance` → `{ "playerId": "...", "balance": 0 }`.

### Phase 2 — Wallet domain + Postgres persistence

1. Migrated grain storage **Redis → Postgres** (`AddAdoNetGrainStorage`, `OrleansDatabaseMigrator`).
2. Grain: `AddFunds`, `DeductFunds`, `GetBalance`; persist on every change.
3. HTTP: `POST /players/{id}/funds`, `POST /players/{id}/funds/deduct`, validation, ProblemDetails.
4. Removed duplicate Orleans config from AppHost.

**Verified:** Add/deduct/balance via API; restart AppHost — balance unchanged; rows in `OrleansStorage`.

### Phase 3 — Kafka integration

1. `AddKafka("kafka")` in AppHost; producer in ApiService.
2. Events on `wallet-events` after successful add/deduct and on deduct rejection (409).
3. Config: `WalletKafka:TopicName` in appsettings.

**Verified:** ApiService logs `Published FundsAdded...`; component test consumes from topic.

### Phase 4 — Component tests

1. `Player Wallet Service.Tests` — xUnit + `Aspire.Hosting.Testing`.
2. Nine tests: root, balance, add, deduct, 409, 400 validation, workflow, Kafka event.

```powershell
dotnet test "Player Wallet Service.Tests\Player Wallet Service.Tests.csproj"
```

Requires Docker Desktop. First run ~30–60s.

### Phase 5 — Performance benchmarks

1. `Player Wallet Service.Benchmarks` — NBomber 6 + NBomber.Http.
2. Scenarios: `get_balance`, `add_funds`, `deduct_funds` — see §5 for results.
3. CLI: `--base-url`, `--rate`, `--minutes`, `--scenario`, `--pool-size`, `--cooldown-seconds`, `--smoke`.

```powershell
# Terminal 1
dotnet run --project "Player Wallet Service.AppHost"

# Terminal 2 — examples
dotnet run --project "Player Wallet Service.Benchmarks" -- --smoke
dotnet run --project "Player Wallet Service.Benchmarks" -- --scenario balance --rate 1000 --minutes 5
dotnet run --project "Player Wallet Service.Benchmarks" -- --scenario add --rate 200 --minutes 5
dotnet run --project "Player Wallet Service.Benchmarks" -- --scenario deduct --rate 200 --minutes 5
```

---

## 5. Implementation Mistakes & Fixes

| # | Mistake | User signal | Fix |
|---|---------|-------------|-----|
| 1 | `UseJsonFormat = true` (removed in Orleans 9) | Build error CS1061 | Removed; use default JSON storage |
| 2 | Double Orleans (`AddOrleans` + `UseOrleans`) | apiservice Unhealthy | Orleans config only in ApiService |
| 3 | `WithHttpHealthCheck` too early | Health check timeout in logs | Removed until readiness endpoint exists |
| 4 | `WaitFor(kafka)` during Phase 2 | Kafka startup warnings | Removed until Phase 3; no WaitFor on apiservice for Kafka |
| 5 | Manual `OrleansGrainStorageSerializer` DI | Silo startup failure | Reverted to default ADO.NET serializer |
| 6 | `[GenerateSerializer]` on grain state with JSON storage | Suspected write failures | Plain POCO state class |
| 7 | POST 500 from bad JSON (PowerShell curl) | curl POST fails, GET works | `BadHttpRequestExceptionHandler`; README `Invoke-RestMethod` examples |
| 8 | PowerShell 5.1 no `-SkipCertificateCheck` | Parameter not found | curl `-k` or HTTP port examples |

---

## 6. Benchmark Results

### First run (2026-05-24) — new GUID per request, 1000 RPS

| Scenario | Duration | Requests | Success | ok RPS | p50 | p95 | p99 |
|----------|----------|----------|---------|--------|-----|-----|-----|
| get_balance | 5:00 | 300,000 | 99.97% | ~999.7 | 3.9 ms | 8.2 ms | 119 ms |
| add_funds | 1:43 | 98,449 | 92.1% | ~880 | 18 ms | 1.2 s | 3.3 s |
| deduct_funds | 0:08 | 7,980 | 0% | 0 | — | — | — |

`add_funds` hit **`SocketException (10055)`** (Windows ephemeral port exhaustion). `deduct_funds` failed immediately after — OS still exhausted.

**Follow-up:** Player pool reuse, cooldown between scenarios, write runs at 200 RPS.

### Final validated runs (2026-05-24) — 5k player pool reuse

| Scenario | Rate | Duration | Requests | Success | p50 | p95 | p99 | Report |
|----------|------|----------|----------|---------|-----|-----|-----|--------|
| get_balance | 1000 RPS | 5:00 | 300,000 | **100%** | 0.7 ms | 1.4 ms | 3.9 ms | `reports/getbalance/nbomber_report_2026-05-24--15-15-37.csv` |
| add_funds | 200 RPS | 5:00 | 60,000 | **100%** | 16.9 ms | 18.6 ms | 26.9 ms | `reports/addfunds/nbomber_report_2026-05-24--15-27-39.csv` |
| deduct_funds | 200 RPS | 5:00 | 60,000 | **100%** | 16.9 ms | 18.1 ms | 22.5 ms | `reports/deductfunds/nbomber_report_2026-05-24--15-34-25.csv` |

### Challenge target vs achieved

| Target | Result |
|--------|--------|
| Read @ 1000 RPS × 5 min | **Met** — 100% success, p95 ~1.4 ms |
| Write @ 1000 RPS × 5 min | **Not met locally** — socket exhaustion + Postgres/Kafka overhead on single Windows machine |
| Write @ 200 RPS × 5 min | **Met** — both add and deduct, 100% success, p95 ~18 ms |

Read path meets the challenge target. Write paths are validated at 200 RPS on local Windows + Docker; full 1k write RPS would need separate load-generator hardware or a cloud environment.

---
