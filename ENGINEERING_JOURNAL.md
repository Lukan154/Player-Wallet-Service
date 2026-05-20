# Engineering Journal — Player Wallet Service

## 1. Approach & Tooling

| Tool | How it's used |
|------|----------------|
| **Cursor (Agent mode)** | Primary implementation: scaffold changes, package installs, AppHost wiring, Orleans grain stub, and this journal. |
| **Aspire CLI / docs** | Referenced for Orleans + Kafka + Postgres integration patterns (`aspire.dev`, Microsoft Learn). |
| **dotnet CLI** | `dotnet add package`, `dotnet build` for dependency and compile verification. |

**Why agentic coding here:** Phase 1 is mostly boilerplate (AppHost resources, NuGet packages, Orleans wiring). AI accelerates that while I validate package versions, connection string names, and build output.

---

## 2. Key Prompts & Iterations

### Prompt (session) — worked on first pass

> Remove Player Wallet Service.Web, create ENGINEERING_JOURNAL.md, start Phase 1 with detailed explanations.

**Why it worked:** Clear scope (delete Web, foundation only), explicit deliverable (journal), and request for explanations (forces documented decisions).

### Iteration — tooling constraint (not AI logic error)

**Problem:** `dotnet` was not on the shell `PATH` in the agent terminal; `dotnet add package` failed with "term not recognized".

**Diagnosis:** PowerShell session lacked `C:\Program Files\dotnet` in PATH (common in automated shells).

**Fix:** Resolved full path `C:\Program Files\dotnet\dotnet.exe` and re-ran package installs successfully.

---

## 3. Architectural Decisions

### 3.1 Remove `Player Wallet Service.Web`

| | |
|---|---|
| **Decision** | Delete the Blazor frontend project and remove it from the AppHost. |
| **Alternatives** | Keep as a demo UI; repurpose for wallet operations. |
| **Why** | Challenge requires HTTP API only. Web was Aspire template noise (weather forecast). Fewer processes = faster `aspire start` and simpler mental model. |

### 3.2 Co-hosted Orleans silo in `ApiService`

| | |
|---|---|
| **Decision** | Run Orleans silo and HTTP API in the same `ApiService` process via `builder.UseOrleans()`. |
| **Alternatives** | Separate silo + API client projects (Aspire `.AsClient()` pattern). |
| **Why** | Challenge allows a single microservice; co-hosting is standard for small services and reduces AppHost complexity in Phase 1. |

### 3.3 Redis for Orleans clustering + grain storage (Phase 1)

| | |
|---|---|
| **Decision** | AppHost: `AddRedis("orleans-redis")` + `AddOrleans().WithClustering(redis).WithGrainStorage("Default", redis)`. ApiService: `AddKeyedRedisClient("orleans-redis")`. |
| **Alternatives** | PostgreSQL via `Orleans.Persistence.AdoNet` (manual); in-memory dev clustering only. |
| **Why** | Aspire's Orleans integration officially supports Redis/Azure for grain storage. Wiring is declarative and env vars inject automatically. Postgres is still provisioned (`walletdb`) for Phase 2+ and journal'd as the target for explicit DB persistence if we migrate off Redis. |

### 3.4 Postgres + Kafka provisioned in AppHost (not yet used in app code)

| | |
|---|---|
| **Decision** | `AddPostgres("postgres").AddDatabase("walletdb")` and `AddKafka("kafka")` with `WithReference` + `WaitFor` on ApiService. |
| **Alternatives** | Add infra only when needed in Phase 2/3. |
| **Why** | Phase 1 goal is "infra boots together." `AddNpgsqlDataSource("walletdb")` registers the client early. Kafka producer comes in Phase 3. |

### 3.5 Stub grain + balance endpoint

| | |
|---|---|
| **Decision** | `IPlayerWalletGrain` / `PlayerWalletGrain` with `GetBalanceAsync()` only; HTTP `GET /players/{playerId}/balance`. |
| **Alternatives** | No HTTP until Phase 2. |
| **Why** | Proves Orleans pipeline end-to-end (grain activation, Redis-backed state, API → grain) before add/deduct logic. |

---

## Phase 1 Log

**Date:** 2026-05-20  
**Status:** Complete (pending local `aspire start` verification on your machine)

**Changes made:**
- Removed `Player Wallet Service.Web` project reference from AppHost.
- AppHost packages: `Aspire.Hosting.PostgreSQL`, `Kafka`, `Orleans`, `Redis` (13.1.0).
- ApiService packages: `Microsoft.Orleans.Server` 9.2.1, Redis clustering/persistence, `Aspire.StackExchange.Redis`, `Aspire.Npgsql`, `Aspire.Confluent.Kafka`.
- Stub grain under `ApiService/Grains/`.
- Orleans OpenTelemetry meters/traces in ServiceDefaults.

**Exit criteria:**
- [x] `dotnet build` succeeds (2026-05-20, 0 errors; NU1902 OpenTelemetry advisories from template ServiceDefaults)
- [ ] `aspire start` — Postgres, Redis, Kafka, ApiService healthy
- [ ] `GET /players/{guid}/balance` returns `{ "playerId": "...", "balance": 0 }`
