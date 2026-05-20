# Player Wallet Service

Aspire-orchestrated player wallet API (Orleans grains, PostgreSQL, Kafka).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Postgres, Redis, Kafka containers)
- [Aspire CLI](https://aspire.dev/reference/cli/overview/) (optional; `dotnet run` on AppHost also works)

## Run locally

```powershell
cd "Player Wallet Service.AppHost"
dotnet run
```

Or with Aspire CLI:

```powershell
aspire run --project "Player Wallet Service.AppHost"
```

Open the Aspire dashboard URL from the console, then call the API (example):

```http
GET https://localhost:{port}/players/3fa85f64-5717-4562-b3fc-2c963f66afa6/balance
```

## Projects

| Project | Role |
|---------|------|
| `Player Wallet Service.AppHost` | Orchestrates Postgres, Redis, Kafka, and ApiService |
| `Player Wallet Service.ApiService` | HTTP API + Orleans silo |
| `Player Wallet Service.ServiceDefaults` | Shared telemetry, health checks, service discovery |

See [ENGINEERING_JOURNAL.md](./ENGINEERING_JOURNAL.md) for design decisions and AI-assisted development notes.
