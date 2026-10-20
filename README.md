# FlowLab

**A visual laboratory for backend performance and resilience.**

[![Validate FlowLab](https://github.com/eturchin/flow-lab/actions/workflows/ci.yml/badge.svg)](https://github.com/eturchin/flow-lab/actions/workflows/ci.yml)

Build a system topology, configure traffic, run a reproducible discrete-event simulation, and compare results. Explore how concurrency limits, queues, dependency failures, retries, and circuit breakers affect latency, throughput, and resource utilization.

**Stack:** React 19 · TypeScript · ASP.NET Core / .NET 10 · PostgreSQL 16 · EF Core 10 · SignalR.

FlowLab is an educational simulator. It does not send load to real services or predict production performance precisely. The application UI is currently in Russian; this README uses English descriptions of its controls.

![FlowLab topology editor, performance metrics, and simulation charts](docs/images/flowlab.png)

## Features

- **Workspaces and scenarios:** organize experiments, edit configurations, and retain run history in PostgreSQL.
- **Topology editor:** model sequential and parallel stages or an explicit directed acyclic graph with broadcast and round-robin routing.
- **Queues and brokers:** explore bounded FIFO/LIFO buffers and shared message queues with competing consumers.
- **Traffic and processing:** configure arrival rates, duration, distributions, worker counts, processing times, variability, error probabilities, and random seeds.
- **Resilience:** simulate dependency degradation, route timeouts, bounded retries with exponential backoff, and per-service circuit breakers.
- **Live experiments:** start or cancel runs and receive progress through SignalR, with HTTP state recovery.
- **Metrics:** inspect throughput, p50/p95/p99 latency, errors, queue sizes, utilization, and node diagnostics.
- **Failure analysis:** browse a paginated request failure journal with arrival/completion times, attempt counts, and failure reasons.
- **Run comparison:** compare two completed runs of the same scenario. Each run stores an immutable configuration snapshot.

Workspace and scenario deletion requires confirmation and is blocked while runs are active. Deleting a scenario also deletes its runs; deleting a workspace removes its scenarios and runs. Demo scenarios are seeded during initial database setup and are not recreated after deletion.

## Quick start with Docker

With Docker and Docker Compose installed, run from the repository root:

```bash
docker compose up --build -d
```

Open **http://localhost:8080**.

Compose starts the application and PostgreSQL. The application port is bound to localhost, and the database port is not published. Database data persists in the `flowlab-data` volume. The application waits for the database health check, applies EF Core migrations, and runs as a non-root user.

```bash
docker compose ps
docker compose logs app
docker compose down
```

`docker compose down` preserves database data. To set a different local database password, copy `.env.example` to `.env` and edit `POSTGRES_PASSWORD` **before the first launch**. Changing this variable does not update the password inside an existing database volume.

The Compose configuration uses `Development` and is intended for local use. The default `postgres` password is a demo credential, not a production secret.

## Local development

### Prerequisites

- .NET SDK 10
- Node.js 22.12 or newer
- PostgreSQL 16, available at `localhost:5432` for the default configuration
- PowerShell for the startup script and the examples below

### Startup script

From the repository root:

```powershell
./start.ps1
```

The script installs frontend dependencies if `node_modules` is missing, builds the React application, and starts ASP.NET Core in Development at **http://localhost:5080**. Unless `ConnectionStrings__FlowLab` is already set, it uses database `flowlab` with username/password `postgres` / `postgres`.

To reuse an existing frontend build:

```powershell
./start.ps1 -SkipFrontend
```

EF Core migrations run automatically at startup. If the `flowlab` database does not exist, the PostgreSQL user needs permission to create it. Alternatively, create it beforehand:

```powershell
# psql must be on PATH; on Windows it is commonly in C:/Program Files/PostgreSQL/16/bin.
$env:PGPASSWORD = 'postgres'
psql -h localhost -U postgres -d postgres -c 'CREATE DATABASE flowlab;'
Remove-Item Env:PGPASSWORD
```

Skip database creation if it already exists. FlowLab does not change the credentials or configuration of an existing PostgreSQL installation.

### Manual startup

```powershell
cd frontend
npm ci
npm run build
cd ..

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__FlowLab = 'Host=localhost;Port=5432;Database=flowlab;Username=postgres;Password=postgres'
dotnet run --project src/FlowLab.Web
```

To apply migrations explicitly before starting the application:

```powershell
dotnet tool restore
dotnet ef database update --project src/FlowLab.Infrastructure --startup-project src/FlowLab.Web
```

For manual or IDE startup, you can also create `src/FlowLab.Web/appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "FlowLab": "Host=localhost;Port=5432;Database=flowlab;Username=postgres;Password=postgres"
  }
}
```

This file is ignored by Git and Docker, loaded only in Development, and excluded from `dotnet publish`. Environment variables take precedence. The startup script sets the connection-string environment variable, so set that variable yourself when using the script with a custom database.

### Rider

Select `FlowLab.Web` and the **http** launch profile from `Properties/launchSettings.json`, then Run/Debug. The profile uses Development and `http://localhost:5080`. After a fresh clone, run `npm ci` and `npm run build` in `frontend` once.

Stop any application instance using port 5080 before launching another from the IDE or terminal. The Docker application uses port 8080.

### Frontend hot reload

Keep the backend running on port 5080, then open a second terminal:

```powershell
cd frontend
npm run dev
```

Open **http://localhost:5173**. Vite proxies API requests and SignalR WebSockets to the backend. In the built application, ASP.NET Core serves the frontend, API, and SignalR together.

## Modeling systems

### Stages, graphs, and message queues

Add services, queues, and external APIs in the editor. In stage mode, adjacent services can run in parallel; the next stage waits for all branches. A Queue node configures the buffer of the **following service**.

Use the connection editor above the topology to convert stages into an explicit graph. Connections determine routing rather than node order. Branches run independently, and a shared downstream node starts once all active predecessors finish. Sources and services support broadcasting to all outgoing branches or selecting one branch in round-robin order. Graphs must be reachable from the source and contain no cycles, with a maximum of 512 connections.

The parallel API and RabbitMQ presets replace the current topology. A Broker node models a shared bounded FIFO/LIFO queue and delivers each message to one available consumer in round-robin order. Consumer worker counts limit concurrent deliveries; busy consumers are skipped. Broker consumers cannot have other incoming connections. To model delivery to every subscriber, use separate brokers representing separate queues.

Broker metrics cover end-to-end job completion, including queue waiting time, rather than the publisher's HTTP response latency. Timeouts and retries apply to the whole route. Durable storage, publisher confirms, nack/requeue, dead-letter queues, and broker recovery are not modeled. Existing scenarios without `connections` continue to use stage mode; graph settings use the existing JSON storage without an additional database migration.

### Suggested experiments

1. **Payment gateway:** run the baseline, increase traffic from 80 to 150 requests/second, and compare p95 latency, queue lengths, and utilization.
2. **Rush hour:** increase the bottleneck Order Service from two workers to eight and rerun with the same seed.
3. **Dependency outage:** simulate an external API outage from second 20 to second 30. Compare the baseline with a circuit breaker, then separately try three attempts and inspect retries, errors, and dependency load.

When comparing runs, the first selected run is the baseline and the second is the candidate. A nonzero relative change from a zero baseline is shown as undefined. The in-app guide explains settings, metrics, and experiments in more detail.

## Architecture

| Project                  | Responsibility                                                                                             |
| ------------------------ | ---------------------------------------------------------------------------------------------------------- |
| `frontend`               | React UI, topology editor, charts, run history, and HTTP/SignalR clients                                   |
| `FlowLab.Web`            | HTTP endpoints, dependency composition, background workers, SignalR hub, health checks, and error handling |
| `FlowLab.Application`    | Use cases, DTOs, interfaces, and run comparison                                                            |
| `FlowLab.Domain`         | Scenario settings, validation rules, and simulation results                                                |
| `FlowLab.Simulation`     | Discrete-event simulation engine                                                                           |
| `FlowLab.Infrastructure` | EF Core persistence, entities, migrations, PostgreSQL, and JSON serialization                              |

Application has no dependency on HTTP, EF Core, or SignalR. Simulation does not depend on ASP.NET Core, the database, or wall-clock time. EF entities stay inside Infrastructure; API responses use DTOs rather than internal event-calendar or request state.

A run reserves capacity, persists its configuration snapshot with status `Queued`, and enters a bounded channel. One of two workers marks it `Running`, executes the simulation, sends throttled progress updates, and saves the result as `Completed`. Errors produce `Failed`; cancellation produces `Cancelled`. Progress is sent at most once every 150 ms. On application restart, interrupted `Queued` and `Running` jobs become `Failed` rather than resuming.

Further documentation, currently in Russian:

- [Architecture decisions](docs/ARCHITECTURE.md)
- [API reference](docs/API.md)
- [Verification notes](docs/VERIFICATION.md)

## Simulation semantics

- **Units:** internal time, timeouts, and delays use milliseconds. Arrival rate and throughput use requests/second. Probabilities range from 0 to 1; displayed utilization and error rates are percentages.
- **Topology:** up to 32 editable nodes, using sequential/parallel stages or an explicit graph. Brokers distribute messages from a shared queue to consumer services.
- **Arrivals:** fixed intervals or exponentially distributed interarrival times (a Poisson process). The first request arrives after the first interval; the generation window excludes its right boundary.
- **Processing:** fixed or exponential service times, with optional uniform variation using a multiplier in `[1-v, 1+v]`. Each run uses its own seeded random generator.
- **Event ordering:** events are ordered by `(virtual time, insertion sequence)`. If a timeout and completion occur at exactly the same time, the timeout wins because it was scheduled first.
- **Timeouts and retries:** a timeout covers the entire route attempt. A retry restarts the route; the attempt count includes the initial attempt. Backoff is `min(maxDelay, initialDelay × 2^(attempt-1))`. Queue overflow fails a request without retrying.
- **Failed attempts:** pending branches are removed from queues using linked-list handles. Processing already in progress retains its resources until completion, but stale results cannot complete a request again. A retry can overlap with work still running from a previous attempt.
- **Circuit breakers:** each service tracks consecutive processing failures, an Open interval, and one HalfOpen probe. Success resets the counter and closes the breaker. Client timeouts and queue overflow do not affect it; old-generation completions do not affect a new probe.
- **Dependency degradation:** the window is evaluated when processing starts, over `[start, start+duration)`. Processing time is multiplied by the configured factor; a full outage produces an error.
- **Drain time:** after the source stops, requests and in-flight processing finish. Throughput and time-weighted averages include this drain period. Charts contain 100 slices of the generation window; chart latency is the mean latency of requests completed in a slice, and chart utilization averages the current busy-worker fractions across services.
- **Aggregates:** p50/p95/p99 use linear interpolation across completed requests, including failures; an empty sample returns zero. Queue averages are time-weighted. The overall average sums station averages; the maximum is the largest queue at an individual node.
- **Reproducibility:** the same seed reproduces the same configuration within the same engine version. Configuration changes can alter random-number consumption. CPU scheduling, networking, garbage collection, database locks, and real-operation side effects are outside the model.

The M/M/1 test uses arrival rate λ = 100/s and service rate μ = 200/s, giving utilization ρ = 0.5. It checks simulation results against theoretical mean time `W = 1/(μ−λ) = 10 ms`, mean queue length `Lq = ρ²/(1−ρ) = 0.5`, and 50% utilization, with tolerance for a finite random sample.

## Build and test

From the repository root:

```powershell
cd frontend
npm ci
npm run build
npm run lint
npm run format:check
npm audit --audit-level=high
cd ..

dotnet build FlowLab.slnx -c Release
dotnet test FlowLab.slnx -c Release --no-build
dotnet format FlowLab.slnx --verify-no-changes --no-restore
```

The .NET tests do not require PostgreSQL. They cover deterministic seeds, event ordering, concurrency, queues, errors, timeout/backoff behavior, circuit breakers, percentiles, comparison, and the M/M/1 reference case.

### API and browser tests

Start the application with PostgreSQL first, preferably using a separate test database. The Playwright suite creates temporary workspaces and scenarios and exercises the running application:

```powershell
cd frontend
npx playwright install chromium
npm run test:e2e
```

The default target is `http://localhost:5080`. Optional overrides:

```powershell
# Use an installed Chrome browser instead of the downloaded Chromium.
$env:PLAYWRIGHT_CHANNEL = 'chrome'
npm run test:e2e

# Test the Docker application.
$env:FLOWLAB_URL = 'http://localhost:8080'
npm run test:e2e
```

Coverage includes CRUD, validation and HTTP status codes, immutable snapshots, deterministic reruns, comparison, cascading deletion, SignalR progress, cancellation, graph editing, brokers, workspace isolation, and complete UI workflows. Layout checks include a 390 px mobile viewport. Screenshots are written to `artifacts/`; failure screenshots and traces are retained in `frontend/test-results/`.

[GitHub Actions](.github/workflows/ci.yml) runs frontend build, lint, formatting, and dependency audit; .NET build, tests, and formatting; and Playwright tests against the application with PostgreSQL.

## Operational behavior and limits

- `GET /health/live` reports process liveness. `GET /health/ready` checks PostgreSQL and returns 503 when unavailable.
- A database connection or migration failure prevents application startup.
- Logs use structured JSON. API errors use Problem Details with a `traceId`; validation runs on the server. Connection passwords are not logged, and EF sensitive-data logging is disabled.
- Two simulations can execute concurrently, with a maximum of ten running and queued jobs combined. Capacity exhaustion returns HTTP 429.
- Scenarios allow up to one million expected requests, with a runtime safety limit of 1.5 million, a maximum generation duration of one hour, and at most ten attempts per request.
- The history API returns the latest 100 runs. Older records remain in the database and can be retrieved individually by ID.
- The application targets a single instance and has no authentication or distributed job queue. Public deployment requires access control, separate secrets and configuration, TLS, and network restrictions. Outside Development, empty database passwords and the demo password `postgres` are rejected.
- Drag-and-drop editing, cyclic graphs, content-based routing, separate HTTP-response/background-job lifecycles, and first-success branch completion are not implemented.
