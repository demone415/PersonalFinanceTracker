# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Finance Tracker** — Full-Stack personal finance app with electronic receipt (QR code) scanning. See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the full architecture plan and [`Подготовка.md`](Подготовка.md) for detailed requirements.

---

## Repository Layout (planned)

```
/
├── backend/          .NET 10 solution (Clean Architecture + Feature Slices)
├── frontend/         React 19.2 SPA (Feature Sliced Design)
├── docker-compose.yml
└── ARCHITECTURE.md   authoritative source for design decisions
```

---

## Git Workflow

### Branching & PRs (mandatory)
- **Every task starts on its own branch off `main`** — never commit task work directly to `main`.
- Branch naming: `<type>/<short-slug>` (e.g. `feat/accrual-crud`, `fix/receipt-retry`), where `<type>` matches the Conventional Commits type.
- If several tasks are requested together, implement them on a **single shared branch** for that batch.
- When development of the task (or batch of tasks) is **finished**, open a **Pull Request into `main`** and **leave it for review** — do not self-merge.

### Commit messages — Conventional Commits (mandatory)
Format: `type(scope): short summary` (imperative, lower-case, no trailing period).

```
feat(accruals): add CRUD endpoints with pagination
fix(receipts): correct NextFetchAt calculation for code 2
docs(architecture): document FIFO receipt queue
chore(ci): add dotnet test step
```

- **Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
- **Scope**: optional, the feature/area (`accruals`, `receipts`, `auth`, `infra`, …).
- Breaking changes: `feat(api)!: …` and/or a `BREAKING CHANGE:` footer.
- Body (optional): explain *what* and *why*, wrap at ~72 chars. Reference tasks/issues in the footer.
- PR titles follow the same Conventional Commits format (the PR title becomes the squash-merge commit subject).

---

## Build & Run Commands

### Full stack
```bash
docker compose up          # starts supabase-db + supabase-auth + redis + rabbitmq + backend + frontend
docker compose up --build  # force rebuild images
```

### Backend
```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/FinanceTracker.Api

# Migrations
dotnet ef migrations add <Name> --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api
dotnet ef database update  --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api
```

### Backend tests
```bash
cd backend
dotnet test                                                   # all tests
dotnet test --filter "FullyQualifiedName~CreateAccrual"       # single test by name
dotnet test --filter "Category=Unit"                          # by trait
dotnet test --logger "console;verbosity=detailed"
```

### Frontend
```bash
cd frontend
npm ci
npm run dev        # Vite dev server → http://localhost:5173
npm run build
npm run lint
npm run type-check
npm test           # vitest
npm test -- --run src/features/accrual-create   # single file
```

---

## Authentication Architecture

Auth is handled by **Supabase GoTrue** (runs as a Docker service), not by the .NET backend.

- Frontend calls GoTrue directly via `@supabase/supabase-js` for login/register/logout
- GoTrue issues HS256 JWTs signed with `SUPABASE_JWT_SECRET`
- .NET API validates the JWT using the same shared secret — no GoTrue call per request
- Users are stored in `auth.users` (managed by GoTrue). App-level user data goes in `public.user_profiles` (FK → `auth.users.id`, managed by EF Core)
- **Never add login/register endpoints to the .NET API** — those belong to GoTrue (`POST /auth/v1/token`, `POST /auth/v1/signup`)
- GoTrue Auth URL: `http://localhost:9999/auth/v1/`
- Supabase Studio (dev admin UI): `http://localhost:3001`
- Role is stored in GoTrue `app_metadata.role` (`"user"` or `"admin"`) and arrives in every JWT
- .NET reads role via `ICurrentUserService.IsAdmin`; services apply `WHERE UserId = currentUser` for regular users and skip the filter for admins
- **Never hard-code UserId filters** in controllers — always go through `ICurrentUserService` so admin bypass works automatically
- **Data isolation via EF Core global query filters**: entities with `UserId` (Accrual, Receipt, Category, MonthlyBudget, ChangeLog, BackgroundTask) carry a global filter `IsAdmin || UserId == currentUser`. Don't duplicate manual `WHERE UserId`; use `IgnoreQueryFilters()` only for intentional admin-wide queries.
- **JWT validation hardening**: pin `HS256`, validate `iss`/`aud`/`exp`. Read role **only** from `app_metadata.role` — never `user_metadata` (user-writable → privilege escalation).
- **Public signup is closed**: users are created by an admin via GoTrue Admin API. No email confirmation, no captcha. Rate-limit the login endpoint.
- **Rate limiting**: ASP.NET Core Rate Limiting globally, with stricter policies on `POST /scan-qr` and login.

---

## Backend Conventions (from Подготовка.md)

### Mandatory patterns
- **Unit of Work**: always use `IUnitOfWork` — never call `DbContext.SaveChanges()` directly outside it.
- **No mapping libraries**: map entities → DTOs manually. No AutoMapper / Mapster.
- **Rich domain models**: business logic lives on entities, not in services/handlers.
- **Collections**: use `ICollection<T>` for mutable, `IReadOnlyCollection<T>` for exposed. Never `null` — initialise as `[]`.
- **Dates**: always `DateTimeOffset`, never `DateTime`.
- **Identifiers**: all primary keys are **GUID v7** (`Guid.CreateVersion7()`, .NET 9+), generated app-side, for B-tree index locality. Note: v7 is time-ordered and therefore **enumerable** — never treat an id as a secret. Resource protection relies on ownership authz + rate-limit; file unguessability comes from the random 256-bit `ResultObjectKey`, not the id.
- **Idempotency**: POST/PUT/DELETE accept `Idempotency-Key: <uuid>` header; store + deduplicate in DB.
- **Refit clients**: use Refit **v10.x** (`Refit` + `Refit.HttpClientFactory`). Wrap with Polly resilience pipeline (retry + circuit breaker).
- **Background jobs**: Hangfire with PostgreSQL persistence. Scheduled retries via `BackgroundJob.Schedule(job, delay)`.
- **Message bus**: Wolverine over RabbitMQ. NuGet packages: `WolverineFx` + `WolverineFx.RabbitMQ` + `WolverineFx.Postgresql` + `WolverineFx.EntityFrameworkCore`.
- **Caching**: FusionCache with Redis backplane. NuGet packages: `ZiggyCreatures.FusionCache` + `ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis` + `ZiggyCreatures.FusionCache.Serialization.SystemTextJson` + `Microsoft.Extensions.Caching.StackExchangeRedis`. Cache dashboard aggregates (TTL 5 min).
- **Logging**: Serilog only; configure sinks in `appsettings.json`, not in code.
- **API versions**: URL-segment versioning (`/api/v1/`, `/api/v2/`). All new endpoints go under `v1` initially.
- **Async import/export**: import/export are **always async** — `POST /accruals/import` and `POST /accruals/export` enqueue a Hangfire job, return `{ jobId }` (202). Status via `GET /jobs/{id}`. The result is **streamed through the service** at `GET /jobs/{id}/result` — **no presigned URLs**: the backend streams the object from a private MinIO bucket, enforces ownership via `BackgroundTask.UserId`, and the endpoint is rate-limited. State lives in `BackgroundTask`. Never do import/export inline in the request.
- **Object storage**: MinIO (S3-compatible), **private** bucket `finance-files`, accessed via the Minio/AWS S3 client. Object keys are cryptographically random opaque tokens (256-bit, never exposed to the client); files are served only by streaming through the API, never by a presigned URL.
- **Health checks**: expose `/health/live` (liveness) and `/health/ready` (readiness with Postgres/Redis/RabbitMQ/MinIO probes).
- **Metrics**: OpenTelemetry → Prometheus `/metrics` (receipt queue length, remaining provider quota, p99 list/dashboard, provider error rate).
- **No Hangfire Dashboard**: do not enable the Hangfire UI; observe jobs via metrics/logs.

### Solution files
- Format: `.slnx` (not `.sln`)
- Central package management: `Directory.Packages.props`
- Shared build properties: `Directory.Build.props`

### No CQRS
Feature Slices use service classes, not Command/Query objects. Each feature has one `*Service.cs` with explicit methods (`CreateAsync`, `UpdateAsync`, `DeleteAsync`, `GetPagedAsync`). Controllers inject services directly via `[FromServices]`. Wolverine is used **only** for async messaging (Outbox/Inbox), not as a synchronous dispatcher.

### Testing
- Framework: xUnit + Moq
- TDD: write failing tests first for new features
- Integration tests must cover: Wolverine bus (producer→consumer round-trip), EF Core + real PostgreSQL (Testcontainers)

---

## Frontend Conventions (from Подготовка.md)

- **Architecture**: Feature Sliced Design — `app / pages / widgets / features / entities / shared`
- **State**: TanStack Query v5 for server state; Zustand for client state (theme, auth, UI).
- **Optimistic updates**: use TanStack Query `optimisticMutation` for create/update/delete.
- **Loading**: show shadcn/ui `Skeleton` components before data arrives — no raw spinners.
- **Theme**: dark by default; Tailwind `darkMode: 'class'`; user toggle stored in Zustand + localStorage.
- **Animations**: Framer Motion for route/component transitions; Lottie for empty states.
- **Forms**: React Hook Form + Zod (schema mirrors backend FluentValidation rules).

---

## Domain Key Concepts

| Term | Meaning |
|------|---------|
| **Accrual** | A financial transaction (income or expense). The central entity. |
| **IncludeInStats** | Bool flag on Accrual — controls inclusion in dashboard/report calculations. |
| **GroupId** | Optional Guid linking related Accruals (e.g. delivery + groceries split across receipts). |
| **Receipt** | Fiscal receipt fetched from ProverkaCheka API, optionally linked to an Accrual. |
| **FetchStatus** | `Pending → Fetched | Failed | RetryLimit` — tracks async receipt retrieval state. |
| **AccrualType** | `Income | ReturnIncome | Expense | ReturnExpense` |

### Receipt fetching flow
1. User scans QR → frontend sends raw QR string to `POST /api/v1/accruals/scan-qr`. Validate QR format (`t=&s=&fn=&i=&fp=&n=`) before enqueuing.
2. API creates Accrual (status Pending) and publishes `ReceiptFetchRequested` via Wolverine
3. Wolverine consumer enqueues the request onto the **global FIFO queue** `receipts` (Hangfire, `WorkerCount = 1` — strictly sequential, never parallel). **Round-robin** across users; per-user daily cap.
4. Hangfire calls `IReceiptProvider` → `ProverkaCheckaProvider` (Refit client). Job is **idempotent** — checks `FetchStatus` first.
5. On code `2` (not yet in tax DB): reschedule per scheme — T+6h, T+1d, T+3d, T+10d (max 5 attempts). On exhaustion / code `3` → **DLQ** + `FetchStatus = RetryLimit`.
6. Rate limit: ≤15 requests/day via Redis `INCR receipt:daily-count:{date}` with end-of-UTC-day TTL. **Redis down → fail-closed**: pause the queue, do not call the provider.
7. User sees progress via `GET /api/v1/accruals/{id}/receipt-status` (Queued+position / Fetched / Failed / RetryLimit).

### ProverkaCheka API response codes
- `0` — invalid receipt
- `1` — success (`data.json` contains receipt)
- `2` — not yet available (reschedule)
- `3` — exceeded 5 retry attempts on this receipt
- `4` — retry too soon (< 10 min since last attempt)
- `5` — server error

---

## Infrastructure URLs (local docker compose)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| Scalar UI | http://localhost:5000/scalar/v1 |
| Health / Metrics | http://localhost:5000/health/ready · /metrics |
| Supabase GoTrue (Auth) | http://localhost:9999/auth/v1/ |
| Supabase Studio (internal only) | http://localhost:3001 |
| RabbitMQ Management (internal only) | http://localhost:15672 |
| MinIO console (internal only) | http://localhost:9001 |

> **Hangfire Dashboard отключён** — admin-UI в публичный контур не выставляем. В production единственный публичный сервис — nginx (TLS/https); Studio, RabbitMQ и MinIO console доступны только во внутренней сети.
