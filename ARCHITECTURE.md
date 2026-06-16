# ARCHITECTURE.md — Трекер личных финансов с поддержкой электронных чеков

> Документ создан до начала кодирования и описывает архитектурные решения, структуру проекта и план разработки.

---

## 1. Обзор проекта

**Finance Tracker** — Full-Stack приложение для ведения личных финансов с поддержкой сканирования QR-кодов фискальных чеков.

**Ключевые возможности:**
- Учёт начислений/расходов (Accruals) с ручным вводом или через сканирование QR-кода чека
- Детализация покупок до позиций чека (из фискальных данных)
- Дашборд с визуализацией расходов и динамики
- Месячные бюджеты с прогресс-баром по категориям
- Импорт из JSON-выгрузки приложения ФНС «Налоги ФЛ», экспорт в CSV
- Мультивалютность и лог изменений
- Многопользовательский режим с изоляцией данных

---

## 2. Технологический стек

### Backend
| Компонент             | Технология                                          |
|-----------------------|-----------------------------------------------------|
| Язык / Платформа      | C# 14, .NET 10                                      |
| Архитектура           | Clean Architecture + Feature Slices                 |
| ORM                   | EF Core 10                                          |
| Шина сообщений        | Wolverine (поверх RabbitMQ) + Outbox/Inbox в PostgreSQL |
| HTTP клиенты          | Refit 10.x + Polly                                  |
| Фоновые задачи        | Hangfire (persistence в PostgreSQL)                 |
| Кэш                   | FusionCache + Redis backplane (4 пакета — см. ниже) |
| Логи                  | Serilog → Console sink                              |
| Метрики               | OpenTelemetry → Prometheus endpoint (`/metrics`)    |
| Health checks         | `Microsoft.Extensions.Diagnostics.HealthChecks` (liveness/readiness) |
| Rate limiting         | ASP.NET Core Rate Limiting middleware               |
| Объектное хранилище   | MinIO (S3-совместимое) через `AWSSDK.S3` / Minio .NET client |
| Валидация             | FluentValidation                                    |
| Тесты                 | xUnit + Moq                                         |
| API документация      | Microsoft.AspNetCore.OpenApi + Scalar UI            |

### Frontend
| Компонент             | Технология                                          |
|-----------------------|-----------------------------------------------------|
| Язык                  | TypeScript 5.9 strict mode                          |
| UI-фреймворк          | React 19.2 strict mode                              |
| Архитектура           | Feature Sliced Design (FSD), SPA без SSR            |
| UI-библиотека         | Tailwind CSS + shadcn/ui                            |
| Состояние             | TanStack Query v5 (серверное) + Zustand v5 (клиентское)|
| Формы                 | React Hook Form + Zod                               |
| Графики               | Recharts                                            |
| Анимации              | Framer Motion + Lottie                              |
| QR сканер             | @zxing/browser (WebRTC камера) + file upload        |
| Роутер                | React Router v7                                     |
| HTTP                  | Axios                                               |

### Инфраструктура
| Компонент    | Технология                                                           |
|--------------|----------------------------------------------------------------------|
| База данных  | Supabase PostgreSQL (`supabase/postgres`)                             |
| Аутентификация | Supabase GoTrue v2.x (JWT issuer, user management)                 |
| Очередь      | RabbitMQ 3 (+ DLQ; management-консоль — только во внутренней сети)   |
| Кэш          | Redis 7                                                              |
| Объектное хранилище | MinIO (S3-совместимое, `pgsty/minio`) — файлы экспорта/импорта |
| Reverse proxy | nginx (production): TLS-терминация, приём `https`, единственный публичный сервис |
| Контейнеры   | Docker Compose (self-hosted Supabase stack)                          |
| Резервные копии | `pg_dump` по расписанию + Supabase PITR; бэкап бакетов MinIO       |
| CI/CD        | GitHub Actions                                                       |

> **Версии Supabase Docker-образов** (`supabase/postgres`, `supabase/gotrue`, `supabase/studio`) берутся из официального файла  
> `docker-compose.yml` в репозитории [supabase/supabase](https://github.com/supabase/supabase/tree/master/docker).  
> Не используй `:latest` — Supabase требует согласованных версий между сервисами.

---

## 3. Доменная модель

### Сущности и связи

```
User (1) ──────────────────────── (N) Accrual
                                         │
                              ┌──────────┼──────────┐
                              │          │          │
                           Receipt      Tag   Category (N:N через AccrualCategory)
                              │
                         ReceiptItem (N:1) ─── Category (N:N)

User (1) ── (N) Category
User (1) ── (N) MonthlyBudget ── (1) Category
User (1) ── (N) ChangeLog
```

### Accrual (начисление / транзакция)
```csharp
Accrual
├── Id                 Guid         PK
├── UserId             Guid         FK → User
├── Amount             decimal      обязательно
├── Date               DateTimeOffset обязательно
├── Type               AccrualType  Income | ReturnIncome | Expense | ReturnExpense
├── Currency           string       ISO 4217, default "RUB"
├── ExchangeRate       decimal?     курс на момент транзакции
├── CategoryId         Guid?        FK → Category
├── Description        string?
├── IncludeInStats     bool         включить в статистику
├── GroupId            Guid?        для группировки связанных транзакций
├── ReceiptId          Guid?        FK → Receipt (nullable)
├── Tags               ICollection<AccrualTag>
└── CreatedAt          DateTimeOffset
```

> **Контракт агрегации по валюте (Epic 8, реализовано).** Каждый агрегат
> приводит сумму операции к **основной валюте** пользователя перед сложением по
> курсу, зафиксированному на момент операции — `Amount × (ExchangeRate ?? 1)`.
> Эталонное правило — `Accrual.AmountInBaseCurrency` (курс `null` означает, что
> операция уже в основной валюте, т.е. 1:1). Эквивалентное выражение продублировано
> **во всех** местах агрегации, и они должны меняться синхронно: `DashboardService`
> (summary, expenses-by-category, monthly-dynamics; top-categories наследует от
> expenses-by-category) и `BudgetService.GetProgressAsync`. Лимиты бюджетов
> считаются заданными в основной валюте. Основная валюта хранится в
> `UserProfile.Currency` и управляется через `GET`/`PUT /api/v1/profile`. Курсы
> хранятся относительно основной валюты **на момент ввода**, поэтому смена основной
> валюты позже не пересчитывает исторические записи.

### Receipt (чек)
```csharp
Receipt
├── Id                 Guid         PK
├── UserId             Guid         FK → User (изоляция данных, global query filter)
├── FD                 long?        Фискальный документ
├── FN                 string?      Фискальный номер
├── FPD                string?      Фискальный признак документа
├── AmountInKopecks    long         обязательно (в копейках)
├── Date               DateTimeOffset обязательно
├── ExternalNumber     string?      номер чека во внешней системе
├── ShiftNumber        int?         номер смены
├── INN                string?
├── Cashier            string?      ФИО кассира
├── Organization       string?
├── Address            string?
├── TaxationType       TaxationType?
├── FetchStatus        ReceiptFetchStatus Pending | Fetched | Failed | RetryLimit
├── FetchAttempts      int          (0–5)
├── NextFetchAt        DateTimeOffset? дата следующей попытки получения
├── RawMetadata        string?      JSON ответа провайдера
└── Items              ICollection<ReceiptItem>
```

### ReceiptItem (позиция чека)
```csharp
ReceiptItem
├── Id                 Guid
├── ReceiptId          Guid         FK → Receipt
├── Name               string       обязательно
├── Price              decimal      обязательно (в рублях)
├── Quantity           decimal      обязательно
├── Sum                decimal      обязательно
└── Categories         ICollection<ReceiptItemCategory>
```

### Category (категория)
```csharp
Category
├── Id                 Guid
├── UserId             Guid?        null = системная категория
├── Name               string
├── Icon               string       код иконки (Lucide)
├── Color              string       HEX цвет
└── IsSystem           bool
```

### MonthlyBudget (месячный бюджет)
```csharp
MonthlyBudget
├── Id                 Guid
├── UserId             Guid
├── CategoryId         Guid
├── Year               int
├── Month              int
├── LimitAmount        decimal
└── Currency           string
```

### TaxationType (вид налогообложения)
```csharp
public enum TaxationType
{
    Osn  = 1,   // ОСН — общая система
    Usn  = 2,   // УСН — упрощённая
    Envd = 4,   // ЕНВД — единый налог на вменённый доход
    Eshn = 8,   // ЕСХН — сельскохозяйственный
    Psn  = 16,  // ПСН — патентная
    Npd  = 32,  // НПД — самозанятые
}
```
Значения соответствуют числовым кодам из API ПроверкаЧека. При маппинге ответа провайдера: `(TaxationType?)data.taxationType`.

### Роли и авторизация

```
Роль    | Хранение                          | Доступ к данным
--------|-----------------------------------|-----------------------------------------
user    | app_metadata.role = "user"        | только свои записи (фильтр по UserId)
admin   | app_metadata.role = "admin"       | все записи всех пользователей
```

**Механизм:**
- Роль хранится в `app_metadata.role` пользователя GoTrue и попадает в JWT-клейм
- .NET-бэкенд читает роль через `ICurrentUserService` (инжектится в сервисы)
- Сервисы не знают про HTTP — только про `ICurrentUserService`

```csharp
public interface ICurrentUserService
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}
```

**Изоляция данных в сервисах:**
```csharp
// AccrualService — паттерн применяется везде
var query = _db.Accruals.AsQueryable();
if (!_currentUser.IsAdmin)
    query = query.Where(a => a.UserId == _currentUser.UserId);
```

**Назначение роли admin** — через GoTrue Admin API (не через .NET):
```bash
PATCH /auth/v1/admin/users/{userId}
{ "app_metadata": { "role": "admin" } }
```
В seed-скрипте это выполняется при первом запуске.

### ChangeLog (лог изменений)
```csharp
ChangeLog
├── Id                 Guid
├── UserId             Guid
├── EntityType         string       "Accrual" | "MonthlyBudget" | ...
├── EntityId           Guid
├── Action             string       "Create" | "Update" | "Delete"
├── Timestamp          DateTimeOffset
├── ValuesBefore       string?      JSON
└── ValuesAfter        string?      JSON
```

### BackgroundTask (статус async импорта/экспорта)
```csharp
BackgroundTask
├── Id                 Guid         PK
├── UserId             Guid         FK → User (изоляция)
├── Type               TaskType     ImportJson | ExportCsv
├── Status             TaskStatus   Pending | Running | Done | Failed
├── Progress           int          0–100
├── ResultObjectKey    string?      криптослучайный opaque-ключ объекта в MinIO (256-бит, не раскрывается клиенту)
├── Error              string?      текст ошибки при Failed
├── CreatedAt          DateTimeOffset
└── CompletedAt        DateTimeOffset?
```
Файлы экспорта/импорта хранятся в приватном бакете MinIO (`finance-files`); скачивание — только через ручку сервиса `GET /jobs/{id}/result` (стрим через backend, проверка владельца, rate-limit). Presigned URL не используется.
Импорт принимает загруженный файл, кладёт в MinIO, ставит фоновую задачу (Hangfire) и возвращает `jobId`.

---

## 4. Архитектура Backend

### Структура Solution

```
FinanceTracker.slnx
├── src/
│   ├── FinanceTracker.Domain/           # Сущности, Value Objects, Domain Events
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── Exceptions/
│   │
│   ├── FinanceTracker.Application/      # Use Cases, Feature Slices
│   │   ├── Common/
│   │   │   ├── Interfaces/              # IUnitOfWork, IReceiptProvider, ICurrentUserService, ...
│   │   │   └── Dtos/                    # Shared DTOs (pagination, etc.)
│   │   └── Features/
│   │       ├── Accruals/
│   │       │   ├── AccrualService.cs
│   │       │   ├── AccrualValidator.cs
│   │       │   └── AccrualDtos.cs
│   │       ├── Categories/
│   │       ├── Budgets/
│   │       ├── Dashboard/
│   │       ├── Receipts/
│   │       ├── Import/
│   │       └── ChangeLog/
│   │
│   ├── FinanceTracker.Infrastructure/   # EF Core, Wolverine, Hangfire, Redis, Refit
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── Configurations/          # EF FluentAPI конфигурации
│   │   │   ├── Migrations/
│   │   │   └── Interceptors/            # ChangeLog EF Interceptor
│   │   ├── Messaging/
│   │   │   └── Messages/                # Wolverine messages
│   │   ├── BackgroundJobs/
│   │   │   └── ReceiptFetchJob.cs       # Hangfire job
│   │   ├── ExternalProviders/
│   │   │   ├── IReceiptProvider.cs
│   │   │   ├── ProverkaCheckaProvider.cs # Refit client
│   │   │   └── ProverkaCheckaModels.cs
│   │   └── Caching/
│   │       └── CacheKeys.cs
│   │
│   └── FinanceTracker.Api/              # ASP.NET Core API
│       ├── Controllers/
│       │   └── V1/
│       │       ├── AccrualsController.cs
│       │       ├── CategoriesController.cs
│       │       ├── BudgetsController.cs
│       │       ├── DashboardController.cs
│       │       └── ChangeLogController.cs
│       ├── Middleware/
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/
│   ├── FinanceTracker.UnitTests/
│   └── FinanceTracker.IntegrationTests/
│
├── Directory.Build.props
└── Directory.Packages.props
```

### Feature Slice структура (пример: Accruals)

```
Features/Accruals/
├── AccrualService.cs        # Create, Update, Delete, GetById, GetPaged
├── AccrualValidator.cs      # FluentValidation rules
├── AccrualDtos.cs           # request/response DTOs (один файл на фичу)
└── AccrualExceptions.cs     # domain-specific exceptions (если нужны)
```

Контроллер инжектит сервис напрямую:
```csharp
// AccrualsController.cs
[HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateAccrualRequest request,
    [FromServices] AccrualService service,
    CancellationToken ct)
    => Ok(await service.CreateAsync(request, ct));
```

### Паттерны

| Паттерн             | Реализация                                                   |
|---------------------|--------------------------------------------------------------|
| Unit of Work        | `IUnitOfWork` → `UnitOfWork(AppDbContext)`, scope per request |
| Repository          | Через `DbSet<T>` внутри DbContext, без отдельного репо-класса |
| Rich Domain Model   | Бизнес-методы в entity, не в сервисах                        |
| Outbox/Inbox        | См. примечание ниже — на M3 сообщения публикуются после коммита, durable Postgres-outbox отложен |
| Idempotency         | `Idempotency-Key` header → хранится в БД, дедупликация       |
| Optimistic Locking  | `RowVersion` на Accrual, Budget                              |
| Идентификаторы (PK) | **GUID v7** (`Guid.CreateVersion7()`, .NET 9+), генерация app-side — упорядоченность для локальности B-tree индекса |

### Аутентификация через Supabase GoTrue

GoTrue — сервис аутентификации из Supabase stack, запускается в Docker Compose.

```
Frontend                  GoTrue                     .NET API
   │  POST /auth/v1/token  │                              │
   │  (login/register)     │                              │
   │ ──────────────────── ►│  issues JWT (HS256)          │
   │ ◄──────────────────── │  secret = SUPABASE_JWT_SECRET│
   │                       │                              │
   │  GET /api/v1/accruals  Bearer <jwt>                  │
   │ ─────────────────────────────────────────────────── ►│
   │                                                      │ validates JWT
   │                                                      │ (shared secret)
```

**Принцип работы:**
- GoTrue хранит пользователей в схеме `auth.users` PostgreSQL
- Выпускает JWT с полем `sub` = UUID пользователя
- .NET API валидирует JWT по общему секрету (`SUPABASE_JWT_SECRET`) — без вызова GoTrue при каждом запросе
- В нашей схеме `public.user_profiles` хранится доп. информация (предпочтения, валюта), FK → `auth.users.id`
- EF Core работает только с `public.*` схемой, не трогает `auth.*`
- Frontend использует `@supabase/supabase-js` для login/register/logout

**Эндпоинты auth (GoTrue, не .NET):**
```
POST  http://localhost:9999/auth/v1/token?grant_type=password   login
POST  http://localhost:9999/auth/v1/signup                      register
POST  http://localhost:9999/auth/v1/logout                      logout
```

### API

- Базовый путь: `/api/v1/`
- Версионирование: через URL-сегмент (`v1`, `v2`)
- Аутентификация: JWT Bearer (токен от GoTrue); валидация — см. §11.1
- Scalar UI: `http://localhost:5000/scalar/v1`
- Hangfire Dashboard: **отключён** — UI не поднимаем; джобы наблюдаются через метрики/логи (§11.7)
- Идемпотентность: POST/PUT/DELETE принимают заголовок `Idempotency-Key: <uuid>`
- Rate limiting: глобально на API + усиленно на `/scan-qr` и login (§11.6)
- Изоляция данных: EF Core global query filters по `UserId`, admin — `IgnoreQueryFilters` (§11.2)

**Основные эндпоинты (.NET API — auth endpoints исключены, они обслуживаются GoTrue):**

```
GET    /api/v1/accruals           ?page,limit,dateFrom,dateTo,categoryId,amountMin,amountMax,type
POST   /api/v1/accruals
GET    /api/v1/accruals/{id}
PUT    /api/v1/accruals/{id}
DELETE /api/v1/accruals/{id}

POST   /api/v1/accruals/scan-qr           сканирование QR → создание Accrual + постановка в FIFO-очередь чека
GET    /api/v1/accruals/{id}/receipt-status   статус подгрузки состава чека (Pending|Queued|Fetched|Failed|RetryLimit + позиция в очереди)

POST   /api/v1/accruals/import            АСИНХРОННЫЙ импорт из ФНС JSON → { jobId } (202 Accepted)
POST   /api/v1/accruals/export            АСИНХРОННЫЙ экспорт в CSV (применяет фильтры) → { jobId } (202 Accepted)

GET    /api/v1/jobs/{id}                  статус фоновой задачи (Pending|Running|Done|Failed + progress)
GET    /api/v1/jobs/{id}/result           стрим файла из MinIO через сервис (rate-limited, проверка владельца); presigned URL не используется

GET    /api/v1/categories
POST   /api/v1/categories
PUT    /api/v1/categories/{id}
DELETE /api/v1/categories/{id}

GET    /api/v1/budgets
POST   /api/v1/budgets
PUT    /api/v1/budgets/{id}
DELETE /api/v1/budgets/{id}
GET    /api/v1/budgets/progress           прогресс бюджетов за текущий месяц

GET    /api/v1/dashboard/summary
GET    /api/v1/dashboard/expenses-by-category   ?year,month
GET    /api/v1/dashboard/monthly-dynamics       ?months=6
GET    /api/v1/dashboard/top-categories         ?limit=5

GET    /api/v1/changelog                 ?entityType,page,limit
```

### Интеграция с ПроверкаЧека

```
┌──────────────┐   QR scan   ┌─────────────┐
│   Frontend   │ ──────────► │    API      │
└──────────────┘             └──────┬──────┘
                                    │ 1. Создать Accrual (без состава)
                                    │ 2. Publish ReceiptFetchRequested (Wolverine)
                                    ▼
                             ┌──────────────┐
                             │  Wolverine   │  RabbitMQ
                             │  Consumer    │
                             └──────┬───────┘
                                    │ 3. Enqueue Hangfire job
                                    ▼
                             ┌──────────────────┐
                             │  ReceiptFetchJob  │  Hangfire
                             └──────┬────────────┘
                                    │ 4. IReceiptProvider.GetReceiptAsync()
                                    ▼
                             ┌──────────────────────┐
                             │  ProverkaCheka API    │
                             │  (Refit + Polly)      │
                             └──────────────────────┘
```

**Глобальная FIFO-очередь к провайдеру (C1 + H1):**
- Все запросы состава чека идут через **одну общую FIFO-очередь**, обрабатываемую строго **последовательно** — выделенная Hangfire-очередь `receipts` с `WorkerCount = 1`. Провайдера не зовём параллельно.
- **Round-robin справедливость между юзерами:** следующий чек выбирается по кругу — по одному от каждого юзера, чтобы один пользователь не монополизировал общий лимит 15/день.
- **Per-user дневной cap** (напр. ≤ 5/день) дополнительно ограничивает монополизацию.
- Юзеру виден статус подгрузки через `GET /accruals/{id}/receipt-status`: `Queued (позиция N)` → `Fetched | Failed | RetryLimit`.

**Ограничение 15 запросов в день:**
- Счётчик хранится в Redis: `receipt:daily-count:{date}`, TTL = до конца текущих суток (UTC)
- Перед каждым запросом: атомарный `INCR` + проверка ≤ 15
- При превышении: запрос остаётся в очереди и переносится на следующий день
- **Redis недоступен → fail-closed (H4):** счётчик недоступен ⇒ провайдера не вызываем, обработку очереди приостанавливаем до восстановления Redis (лимит провайдера важнее доступности подгрузки)

**Стратегия повторных попыток (Схема 2, код `2` — чек ещё не в БД ФНС):**
```
Попытка 1: сразу (T)
Попытка 2: T + 6 часов
Попытка 3: T + 1 день
Попытка 4: T + 3 дня
Попытка 5: T + 10 дней
```
- Реализация через `Hangfire.Schedule(job, delay)`; состояние — в `Receipt.FetchAttempts` и `Receipt.NextFetchAt`.
- **Идемпотентность джобы (M2):** перед вызовом провайдера джоба проверяет `FetchStatus` — повторный/дублирующий запуск не плодит дублей и лишних вызовов.
- **Dead-letter queue (M3):** исчерпание 5 попыток (или код `3`) → сообщение уходит в DLQ (Wolverine error handling / RabbitMQ DLX), `Receipt.FetchStatus = RetryLimit`, статус виден юзеру.
- Два механизма отсрочки композируются: «квота на сегодня исчерпана» (перенос на завтра) vs «код 2 — ещё не готов» (схема T+6h/+1d/…).

---

## 5. Архитектура Frontend

### Feature Sliced Design (FSD) структура

```
src/
├── app/                    # Инициализация, роутер, провайдеры, тема
│   ├── providers/
│   ├── router.tsx
│   └── store/              # Zustand: auth, theme, global UI state
│
├── pages/                  # Страницы-маршруты
│   ├── DashboardPage/
│   ├── AccrualsPage/
│   ├── AccrualDetailPage/
│   ├── CategoriesPage/
│   ├── BudgetsPage/
│   ├── ChangeLogPage/
│   └── LoginPage/
│
├── widgets/                # Составные блоки (используют features)
│   ├── AccrualList/
│   ├── DashboardCharts/
│   ├── BudgetProgressList/
│   └── Navbar/
│
├── features/               # Функциональные блоки с бизнес-логикой
│   ├── accrual-create/     # Форма создания + сканер QR
│   ├── accrual-edit/
│   ├── accrual-filter/
│   ├── receipt-scan/       # QR сканирование
│   ├── category-manage/
│   ├── budget-manage/
│   ├── import-export/
│   └── auth/
│
├── entities/               # Бизнес-сущности (типы, хуки запросов, карточки)
│   ├── accrual/
│   ├── category/
│   ├── budget/
│   └── receipt/
│
└── shared/                 # UI kit, утилиты, API клиент
    ├── ui/                 # shadcn/ui компоненты + кастомные
    ├── api/                # Axios instance, инвалидация кэша
    ├── hooks/
    └── lib/
```

### Ключевые UI/UX решения

| Требование              | Реализация                                               |
|-------------------------|----------------------------------------------------------|
| Оптимистичные обновления| TanStack Query `optimisticMutations`                     |
| Скелетоны загрузки      | shadcn/ui Skeleton компоненты                            |
| Тёмная тема (default)   | Tailwind `darkMode: 'class'`, Zustand хранит выбор       |
| Адаптивность            | Tailwind responsive breakpoints, mobile-first            |
| Анимации                | Framer Motion для переходов + Lottie для пустых состояний|
| QR сканирование         | @zxing/browser (камера) + file input                     |
| Чарты                   | Recharts (PieChart, LineChart, BarChart)                 |

### Цветовая палитра (CSS custom properties)

Тема задаётся CSS-переменными в oklch-пространстве. Подключаются глобально в `app/styles/globals.css` и доступны через Tailwind как `var(--<token>)`.

```css
/* Фоновые слои (от тёмного к светлому) */
--bg-dark:      oklch(0.1  0.005 255);  /* самый тёмный фон — модалки, оверлеи */
--bg:           oklch(0.15 0.005 255);  /* основной фон страницы               */
--bg-light:     oklch(0.2  0.005 255);  /* карточки, панели                    */

/* Текст */
--text:         oklch(0.96 0.01  255);  /* основной текст                      */
--text-muted:   oklch(0.76 0.01  255);  /* второстепенный текст, плейсхолдеры  */

/* Разделители и акценты */
--highlight:    oklch(0.5  0.01  255);  /* выделение строк, hover-подсветка    */
--border:       oklch(0.4  0.01  255);  /* основные границы                    */
--border-muted: oklch(0.3  0.01  255);  /* тонкие разделители                  */

/* Семантические цвета */
--primary:      oklch(0.76 0.1   255);  /* основной акцент (синий)             */
--secondary:    oklch(0.76 0.1    75);  /* вторичный акцент (жёлто-зелёный)    */
--danger:       oklch(0.7  0.05   30);  /* ошибки, удаление                    */
--warning:      oklch(0.7  0.05  100);  /* предупреждения                      */
--success:      oklch(0.7  0.05  160);  /* успех, положительный баланс         */
--info:         oklch(0.7  0.05  260);  /* информационные сообщения            */
```

**Соответствие доменным понятиям:**
| Элемент UI                       | Токен              |
|----------------------------------|--------------------|
| Доходы / положительный баланс    | `--success`        |
| Расходы / превышение бюджета     | `--danger`         |
| Прогресс бюджета (≥80%)          | `--warning`        |
| Статус «Ожидание» чека           | `--info`           |
| Основная кнопка / ссылка         | `--primary`        |
| Категория (дополнительный тон)   | `--secondary`      |

---

## 6. Инфраструктура и Docker Compose

```yaml
# docker-compose.yml (схема)
services:
  # ── Supabase stack ───────────────────────────────────────────
  supabase-db:
    image: supabase/postgres:15.1.0.147
    # PostgreSQL с Supabase расширениями
    # Содержит схему auth.* (GoTrue) и public.* (приложение)

  supabase-auth:
    image: supabase/gotrue:v2.143.0
    depends_on: [supabase-db]
    ports: ["9999:9999"]
    environment:
      GOTRUE_JWT_SECRET: ${SUPABASE_JWT_SECRET}
      GOTRUE_DB_DATABASE_URL: postgres://...
      GOTRUE_SITE_URL: http://localhost:3000
    # Auth API: http://localhost:9999/auth/v1/

  supabase-studio:
    image: supabase/studio:latest
    ports: ["3001:3000"]            # dev: на localhost допустимо; prod: НЕ публиковать наружу
    # Supabase Studio и RabbitMQ-консоль — административные панели; в production доступ
    # только через внутреннюю сеть / VPN / SSH-туннель, не через публичный nginx

  # ── Приложение ───────────────────────────────────────────────
  redis:
    image: redis:7-alpine
    # FusionCache backplane + rate limit счётчики

  rabbitmq:
    image: rabbitmq:3-management
    ports: ["15672:15672"]          # dev only; prod: management-консоль во внутренней сети
    # Шина сообщений + DLQ для терминальных ошибок подгрузки чека

  minio:
    image: quay.io/minio/minio       # S3-совместимое хранилище (pgsty/minio)
    command: server /data --console-address ":9001"
    ports: ["9000:9000", "9001:9001"]   # 9000 — S3 API, 9001 — консоль (prod: консоль во внутр. сети)
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    # Бакет finance-files: файлы экспорта/импорта

  backend:
    build: ./backend
    depends_on: [supabase-db, supabase-auth, redis, rabbitmq, minio]
    ports: ["5000:8080"]
    environment:
      SUPABASE_JWT_SECRET: ${SUPABASE_JWT_SECRET}
      MINIO_ENDPOINT: http://minio:9000
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    # Scalar UI:  http://localhost:5000/scalar/v1
    # Health:     /health/live, /health/ready    Метрики: /metrics
    # Hangfire Dashboard отключён

  frontend:
    build: ./frontend
    depends_on: [backend, supabase-auth]
    ports: ["3000:80"]
    environment:
      VITE_SUPABASE_URL: http://localhost:9999
      VITE_SUPABASE_ANON_KEY: ${SUPABASE_ANON_KEY}   # @supabase/supabase-js v2.x
      VITE_API_URL: http://localhost:5000

  # ── Production-only ──────────────────────────────────────────
  nginx:
    image: nginx:alpine
    ports: ["443:443", "80:80"]
    # Reverse proxy: TLS-терминация, приём https. ЕДИНСТВЕННЫЙ сервис в публичном контуре.
    # Внутренние панели (Studio, RabbitMQ, MinIO console) наружу не выставляются.

  # Резервные копии (M4): cron pg_dump → MinIO/внешнее хранилище + Supabase PITR; бэкап бакетов MinIO
```

### CI/CD (GitHub Actions)
```yaml
# .github/workflows/ci.yml
on: [push, pull_request]
jobs:
  backend:
    - dotnet restore
    - dotnet build --no-restore
    - dotnet test --no-build
  frontend:
    - npm ci
    - npm run lint
    - npm run type-check
    - npm run test
```

---

## 7. Тесты

### Unit тесты (`FinanceTracker.UnitTests`)
| # | Тест                                                           |
|---|----------------------------------------------------------------|
| 1 | CreateAccrualCommandValidator — валидные данные проходят       |
| 2 | CreateAccrualCommandValidator — сумма = 0 не проходит          |
| 3 | AccrualType — тип Expense корректно учитывается в статистике   |
| 4 | ReceiptFetchRateLimiter — превышение лимита 15 возвращает false|
| 5 | Receipt retry schedule — правильно вычисляет NextFetchAt       |
| 6 | BudgetProgressCalculator — прогресс рассчитывается верно       |
| 7 | DashboardQueryHandler — топ-5 категорий возвращает <= 5        |
| 8 | Category validator — пустое имя не проходит                    |
| 9 | CsvExporter — генерирует корректный CSV                        |
|10 | JsonImporter — парсит формат ФНС и создаёт транзакции          |

### Integration тесты (`FinanceTracker.IntegrationTests`)
| # | Тест                                                          |
|---|---------------------------------------------------------------|
|11 | Wolverine message bus — ReceiptFetchRequested доходит до consumer |
|12 | EF Core + PostgreSQL — Accrual сохраняется с тегами           |

---

## 8. Seed-данные

| Сущность    | Кол-во | Описание                                          |
|-------------|--------|---------------------------------------------------|
| Users       | 3      | `user@personal.ru` (user) / `user@family.ru` (user) / `admin@finance.ru` (admin) |
| Categories  | 12     | Продукты, Транспорт, Кафе, Здоровье, ЖКХ, ...    |
| Accruals    | 200+   | За последние 6 месяцев, оба пользователя          |
| Budgets     | 3      | Для разных категорий                              |
| Receipts    | 10+    | Привязаны к части транзакций                      |

---

## 9. План разработки (Epics → Stories → Tasks)

### Epic 1: MVP — базовая инфраструктура + Auth + Начисления + Категории

**Story 1.1: Инициализация проекта**
- [x] T1.1.1 Создать .NET solution (`.slnx`, `Directory.Build.props`, `Directory.Packages.props`)
- [x] T1.1.2 Добавить проекты: Domain, Application, Infrastructure, Api
- [x] T1.1.3 Настроить EF Core + AppDbContext + UnitOfWork
- [x] T1.1.4 Настроить Serilog + appsettings.json
- [x] T1.1.5 Настроить Microsoft.AspNetCore.OpenApi + Scalar UI с версионированием
- [x] T1.1.6 Создать Docker Compose (postgres + redis + rabbitmq + backend + frontend)
- [x] T1.1.7 Инициализировать frontend (Vite + React 19 + TypeScript 5.9 + Tailwind + shadcn/ui)
- [x] T1.1.8 Настроить FSD структуру папок frontend
- [x] T1.1.9 Поднять MinIO (S3) в Docker Compose + бакет `finance-files` + клиент в Infrastructure
- [x] T1.1.10 Health checks (`/health/live`, `/health/ready`) с пробами Postgres/Redis/RabbitMQ; liveness/readiness probes
- [x] T1.1.11 Глобальный rate limiting middleware (ASP.NET Core) + усиленные политики на `/scan-qr` и login
- [x] T1.1.12 EF Core global query filters по `UserId` (admin — `IgnoreQueryFilters`)
- [x] T1.1.13 OpenTelemetry + Prometheus endpoint `/metrics`
- [ ] T1.1.14 nginx reverse proxy (TLS/https) — production-конфиг; убрать публичные порты внутренних панелей _(отложено по решению заказчика — production-контур пока не разворачиваем)_

**Story 1.2: Аутентификация и роли (Supabase GoTrue)**
- [x] T1.2.1 Настроить GoTrue сервис в Docker Compose (переменные, DB URL, JWT secret)
- [x] T1.2.2 Настроить .NET JWT-валидацию по `SUPABASE_JWT_SECRET`: пиннинг `HS256`, проверка `iss/aud/exp`; роль — строго из `app_metadata.role` (никогда из `user_metadata`)
- [x] T1.2.3 Реализовать `ICurrentUserService` + `CurrentUserService` (читает UserId и IsAdmin из HttpContext)
- [x] T1.2.4 Создать `public.user_profiles` таблицу + EF Core entity, FK → `auth.users.id`
- [x] T1.2.5 Seed 3 пользователей (2 user + 1 admin) через GoTrue Admin API; назначить `app_metadata.role`
- [x] T1.2.6 Настроить `@supabase/supabase-js` клиент на фронтенде
- [x] T1.2.7 Страница Login (форма через Supabase client SDK)
- [x] T1.2.8 Auth context (Zustand) — хранить UserId, роль, сессию; ProtectedRoute + AdminRoute
- [x] T1.2.9 Закрыть публичный signup в GoTrue; регистрацию юзеров делает админ через Admin API. Email-подтверждение и капчу не включаем
- [x] T1.2.10 Rate-limit на login (защита от брутфорса / credential stuffing)

**Story 1.3: Категории**
- [x] T1.3.1 Category entity + миграция + seed 12 категорий
- [x] T1.3.2 CRUD эндпоинты /api/v1/categories с FluentValidation
- [x] T1.3.3 Страница категорий: список + создание + редактирование + удаление
- [x] T1.3.4 Выбор иконки (Lucide) и цвета HEX в форме

**Story 1.4: Начисления (Accruals) CRUD**
- [x] T1.4.1 Accrual, ReceiptItem, AccrualTag entities + миграции
- [x] T1.4.2 CRUD эндпоинты /api/v1/accruals с пагинацией и FluentValidation
- [x] T1.4.3 Поддержка тегов и GroupId
- [x] T1.4.4 ChangeLog EF Interceptor (значения до/после в JSON)
- [x] T1.4.5 Список начислений с пагинацией, скелетон-загрузка
- [x] T1.4.6 Форма создания/редактирования начисления (оптимистичные обновления)
- [x] T1.4.7 Ручное добавление позиций чека к начислению

---

### Epic 2: Дашборд и визуализация

**Story 2.1: Dashboard endpoints**
- [x] T2.1.1 GET /dashboard/summary (общий баланс, доходы/расходы за месяц)
- [x] T2.1.2 GET /dashboard/expenses-by-category (данные для круговой диаграммы)
- [x] T2.1.3 GET /dashboard/monthly-dynamics (6 месяцев)
- [x] T2.1.4 GET /dashboard/top-categories (топ-5)
- [x] T2.1.5 Кэширование ответов Dashboard через FusionCache (TTL 5 мин), ключи **с UserId**; инвалидация при изменении начислений

**Story 2.2: Dashboard UI**
- [x] T2.2.1 Страница Dashboard с компоновкой виджетов
- [x] T2.2.2 PieChart расходов по категориям (Recharts)
- [x] T2.2.3 LineChart динамики за 6 месяцев (Recharts)
- [x] T2.2.4 Топ-5 категорий (горизонтальный BarChart)
- [x] T2.2.5 Сводные карточки (доход, расход, баланс месяца)

---

### Epic 3: Фильтрация и поиск

**Story 3.1: Фильтры начислений**
- [x] T3.1.1 Backend: query params dateFrom/dateTo, categoryId, amountMin/Max, type
- [x] T3.1.2 Frontend: компонент FilterPanel
- [x] T3.1.3 Синхронизация фильтров с URL query params

---

### Epic 4: Сканирование QR-чеков

**Story 4.1: Провайдер ПроверкаЧека**
- [x] T4.1.1 Интерфейс `IReceiptProvider` + Refit клиент `ProverkaCheckaClient`
- [x] T4.1.2 Polly политики (retry + circuit breaker) для Refit клиента
- [x] T4.1.3 Rate limiter (≤15/день) через Redis INCR + TTL; **fail-closed при недоступности Redis**
- [x] T4.1.4 Маппинг ответа API → Receipt + ReceiptItems (вручную)
- [x] T4.1.5 Валидация строки QR (формат `t=&s=&fn=&i=&fp=&n=`) до постановки в очередь
- [x] T4.1.6 Добавить `Receipt.UserId` + per-user дневной cap на запросы к провайдеру

**Story 4.2: Фоновая обработка чеков**
- [x] T4.2.1 Wolverine message `ReceiptFetchRequested` + consumer handler; DLQ для терминальных ошибок
- [x] T4.2.2 Глобальная FIFO-очередь `receipts` (`WorkerCount = 1`, последовательная обработка) + round-robin между юзерами
- [x] T4.2.3 Hangfire job `ReceiptFetchJob` с Schedule (схема 2: +6ч, +1д, +3д, +10д); идемпотентность по `FetchStatus`
- [x] T4.2.4 Обновление `Receipt.FetchStatus` после успеха/ошибки/лимита; перевод в `RetryLimit` → DLQ

**Story 4.3: Сканер на фронтенде**
- [x] T4.3.1 QrScanner компонент (камера через @zxing/browser + загрузка файла)
- [x] T4.3.2 Поток: скан QR → POST /accruals/scan-qr → создание начисления
- [x] T4.3.3 Индикатор статуса подгрузки чека (`GET /accruals/{id}/receipt-status`): Queued (позиция) / Получен / Ошибка / Лимит попыток

---

### Epic 5: Месячные бюджеты

**Story 5.1: Бюджеты**
- [x] T5.1.1 Budget entity + миграция + seed 3 бюджета
- [x] T5.1.2 CRUD эндпоинты /api/v1/budgets с FluentValidation
- [x] T5.1.3 GET /budgets/progress — расчёт процента расхода бюджета
- [x] T5.1.4 Страница бюджетов с прогресс-барами по категориям
- [x] T5.1.5 Форма создания/редактирования бюджета

---

### Epic 6: Импорт / Экспорт

**Story 6.1: Импорт из ФНС JSON (асинхронно)**
- [ ] T6.1.1 Парсер JSON формата приложения «Налоги ФЛ»
- [ ] T6.1.2 POST /accruals/import → загрузка файла в MinIO + фоновая задача (Hangfire) → возвращает `jobId`
- [ ] T6.1.3 Сущность `BackgroundTask` (статус/прогресс) + GET /jobs/{id}
- [ ] T6.1.4 UI: загрузка файла + опрос статуса задачи + отображение результата

**Story 6.2: Экспорт в CSV (асинхронно)**
- [ ] T6.2.1 POST /accruals/export (применяет фильтры) → фоновая задача → CSV в приватный бакет MinIO (object key — криптослучайный токен)
- [ ] T6.2.2 GET /jobs/{id}/result — стрим файла через сервис: проверка владельца (`BackgroundTask.UserId`) + rate-limit; presigned URL не используем
- [ ] T6.2.3 Кнопка «Экспорт» в списке: запуск задачи + уведомление о готовности

---

### Epic 7: Лог изменений

**Story 7.1: Change Log**
- [x] T7.1.1 GET /changelog с пагинацией и фильтром по entityType
- [x] T7.1.2 Страница журнала изменений

---

### Epic 8: Мультивалютность

**Story 8.1: Валюты**
- [x] T8.1.1 Поля Currency + ExchangeRate в Accrual entity
- [x] T8.1.2 Выбор основной валюты в профиле пользователя (`UserProfile.Currency`, `GET`/`PUT /api/v1/profile`, страница «Настройки»)
- [x] T8.1.3 Хранение курса на момент транзакции (`Accrual.ExchangeRate`; форма требует курс для иностранной валюты)
- [x] T8.1.4 Конвертация в основную валюту во **всех** агрегатах — дашборд **и** прогресс бюджетов (`BudgetService.GetProgressAsync`); см. «Контракт агрегации по валюте» у Accrual

---

### Epic 9: Нефункциональные требования

- [ ] T9.1 Seed-данные: 200+ транзакций, 12 категорий, 3 бюджета, 2 пользователя
- [ ] T9.2 ≥10 unit тестов + ≥2 integration тестов
- [ ] T9.3 GitHub Actions CI pipeline (lint + тесты)
- [ ] T9.4 README.md с инструкцией по запуску
- [ ] T9.5 Вести REPORT.md в процессе разработки
- [ ] T9.6 Индексы: `(UserId, Date DESC)` для ленты + индексы по фильтрам (CategoryId, Type, Amount); проекция в DTO через `.Select()` (без N+1)
- [ ] T9.7 Метрики: длина очереди чеков, остаток дневной квоты провайдера, p99 ленты/дашборда, ошибки провайдера; алерты
- [ ] T9.8 Резервное копирование: pg_dump по расписанию + Supabase PITR; бэкап бакетов MinIO
- [ ] T9.9 Health checks + liveness/readiness пробы в Compose / оркестраторе

---

### Epic 10: Повторяющиеся транзакции _(поздний этап)_

**Story 10.1: Recurring Accruals**
- [ ] T10.1.1 Сущность `RecurringTemplate` (шаблон: Amount, Category, Type, Description, CronExpression)
- [ ] T10.1.2 CRUD эндпоинты /api/v1/recurring-templates
- [ ] T10.1.3 Hangfire recurring job — создаёт Accrual из шаблона по расписанию
- [ ] T10.1.4 Страница управления шаблонами повторяющихся транзакций
- [ ] T10.1.5 Отметка автоматически созданных Accruals (`SourceTemplateId`)

---

## 10. Ключевые архитектурные решения и обоснование

| Решение | Обоснование |
|---------|-------------|
| Supabase GoTrue для Auth | Полноценный auth-сервис с управлением сессиями; .NET только валидирует JWT по shared secret — нет lock-in на ASP.NET Identity |
| Clean Architecture + Feature Slices (без CQRS) | Слои — тестируемость; Feature Slices — минимальный coupling; сервисы проще, чем Command/Query объекты |
| Wolverine (не MassTransit) | Нативная интеграция с EF Core для Outbox (план), простая настройка дюрабельности и DLQ; на M3 outbox отложен — см. примечание ниже |
| FusionCache + Redis | In-memory + Redis backplane = кэш выживает при рестарте |
| Ручной маппинг (не AutoMapper) | Явная типобезопасность, проще отлаживать, нет reflection оверхеда |
| TanStack Query + Zustand | TQ — серверное состояние (кэш, инвалидация); Zustand — клиентское UI состояние |
| ICollection / IReadOnlyCollection | Инкапсуляция коллекций, запрет внешних мутаций |
| DateTimeOffset вместо DateTime | Хранит timezone offset, корректно для финансовых записей |
| GUID v7 для всех PK | Упорядоченность по времени → локальность B-tree индекса (быстрее v4); id не секрет — защита ресурсов через authz + rate-limit + случайный object key |
| Idempotency Key | Защита от дублирования транзакций при retry на клиенте |
| Supabase PostgreSQL для Hangfire + Wolverine | Единая БД, нет дополнительных зависимостей |
| **Durable Postgres-outbox отложен (M3)** | `WolverineFx.Postgresql`/`.EntityFrameworkCore` тянут `Weasel.Postgresql` → `Npgsql 9`, что конфликтует с `Npgsql 10` (EF Core 10). Транзакционный outbox к тому же требует, чтобы публикация участвовала в EF-транзакции продьюсера (`IDbContextOutbox`/middleware Wolverine), а это завязано на Wolverine-handler'ы — наш продьюсер `ReceiptScanService` обычный сервис. Поэтому `ReceiptFetchRequested` публикуется **после** коммита, а долговечность обеспечивается строкой `Receipt` (источник истины) + recurring-диспетчером: потерянное «wake-up»-сообщение восстанавливается следующим проходом sweep. Очередь подключена только `WolverineFx` + `WolverineFx.RabbitMQ`. Вернуться к durable-outbox — когда Weasel перейдёт на Npgsql 10. |
| Глобальная FIFO-очередь + round-robin (1 воркер) | Не превышаем лимит провайдера, не зовём API параллельно, честное распределение между юзерами (новые сканы входят через round-robin dispatch, не прямым enqueue) |
| Async импорт/экспорт через MinIO + BackgroundTask | Тяжёлые операции вне request-потока; файлы — в приватном S3-бакете; скачивание через ручку сервиса (стрим + проверка владельца + rate-limit), без presigned URL; object key — криптослучайный токен |
| EF Core global query filters | Изоляция по UserId по умолчанию, а не дисциплиной разработчика; admin — IgnoreQueryFilters |
| Redis fail-closed для квоты провайдера | Потеря счётчика не должна приводить к превышению лимита и бану ключа |
| Feature-gate загрузки чеков по токену провайдера | Нет токена ProverkaCheka → сканирование отключено целиком: `scan-qr` отдаёт `503` с кодом `receipt_scanning_disabled`, фоновые чеки в очереди ставятся на паузу (не жгут retry-бюджет), а `GET /api/v1/capabilities` (`{ receiptScanning, fnsImport }`) сообщает фронту, что выключить. **Флаг проверяется в рантайме**: токен можно добавить на бэке независимо от деплоя фронта, поэтому SPA не хардкодит доступность, а опрашивает `capabilities`. `fnsImport` намеренно завязан на тот же токен (единый «раздел чеков»), хотя импорт из ФНС технически провайдер не вызывает — расцепить, если импорт понадобится отдельно. |
| nginx как единственный публичный сервис | Внутренние панели (Studio, RabbitMQ, MinIO console) не выставляются в интернет; TLS на nginx |
| Hangfire Dashboard отключён | Не выставляем админ-UI в публичный контур; наблюдаемость — через метрики/логи |

---

## 11. Безопасность, масштабируемость, отказоустойчивость

> Раздел консолидирует решения по требованиям интернет-доступа, скорости чтения,
> асинхронности тяжёлых операций, изоляции данных и устойчивости очереди чеков.

### 11.1 Аутентификация и валидация JWT (C5)
- Валидация GoTrue-JWT по shared secret `SUPABASE_JWT_SECRET` с **пиннингом алгоритма `HS256`** (отвергаем `alg: none` и подмену), проверкой `iss`, `aud`, `exp`, подписи.
- Роль читается **строго из `app_metadata.role`** (server-controlled). `user_metadata` юзер меняет сам — оттуда роль не берём никогда (иначе эскалация до admin).
- `ICurrentUserService` отдаёт `UserId` (`sub`) и `IsAdmin`; HTTP-слой инкапсулирован.
- ⚠️ **На будущее — переход на асимметричную подпись (RS256/ES256):** сейчас HS256 + общий секрет в конфиге обеих сторон. Опционально перейти на асимметричные ключи GoTrue: приватный ключ только у GoTrue, публичный — в `/.well-known/jwks.json`, а `JwtBearer` валидирует через `Authority`/`MetadataAddress` (авто-загрузка и кэш JWKS). Плюсы: ротация ключей без передеплоя API, секрет не хранится на бэкенде. Минусы: сетевая зависимость от GoTrue при старте/refresh метаданных, чуть сложнее локалка. Пока остаёмся на HS256.

### 11.2 Изоляция данных — global query filters (H2)
- На всех сущностях с `UserId` (Accrual, Receipt, Category, MonthlyBudget, ChangeLog, BackgroundTask) — **EF Core global query filter** `e => _currentUser.IsAdmin || e.UserId == _currentUser.UserId`.
- Изоляция работает по умолчанию; ручной `WHERE UserId` больше не требуется и не должен дублироваться.
- Админ-сценарии — явный `IgnoreQueryFilters()` там, где это намеренно.
- `Receipt` получил собственный `UserId` (C4) — прямой доступ к чеку/составу больше не зависит от джойна с Accrual.

### 11.3 Производительность чтения < 1 c (M1, H3)
- Индексы: `(UserId, Date DESC)` для ленты начислений; индексы по столбцам фильтров (`CategoryId`, `Type`, `Amount`).
- Чтение — **проекция в DTO через `.Select()`** прямо в SQL, без загрузки графов и без N+1 на тегах/категориях.
- Дашборд: агрегаты считаются в БД (`GROUP BY`), кэшируются в FusionCache (TTL 5 мин). **Ключи кэша включают `UserId`** (`dashboard:summary:{userId}:{period}`) — нет утечки агрегатов между юзерами. Инвалидация ключей юзера при изменении его начислений. Защита от cache stampede — встроенная в FusionCache.

### 11.4 Очередь подгрузки чеков (C1, H1, H4, M2, M3)
См. §4 «Интеграция с ПроверкаЧека». Кратко: глобальная FIFO-очередь `receipts`, 1 воркер (последовательно), round-robin между юзерами, per-user дневной cap, Redis-счётчик 15/день с **fail-closed**, идемпотентная джоба, DLQ + статус `RetryLimit`, статус для юзера через `GET /accruals/{id}/receipt-status`.

### 11.5 Health checks и reverse proxy (H5)
- `/health/live` (liveness) и `/health/ready` (readiness) с пробами **Postgres, Redis, RabbitMQ** (и MinIO).
- Пробы используются Docker Compose / оркестратором для рестарта и трафика.
- В production перед приложением — **nginx** (reverse proxy): TLS-терминация, приём `https`, единственный публичный сервис. Внутренние панели (Studio, RabbitMQ, MinIO console) наружу не выставляются.

### 11.6 Rate limiting и регистрация (M6, C1)
- Глобальный ASP.NET Core Rate Limiting на API; усиленные лимиты на `POST /scan-qr` (защита общей квоты провайдера от монополизации) и на login (брутфорс / credential stuffing).
- **Публичный signup закрыт** — юзеров регистрирует админ через GoTrue Admin API. Email-подтверждение и капча не включаются (по решению заказчика).

### 11.7 Метрики и наблюдаемость (M8)
- OpenTelemetry → Prometheus `/metrics`. Ключевые метрики: длина очереди чеков, остаток дневной квоты провайдера, p99 ленты/дашборда, доля ошибок провайдера, время выполнения async-задач.
- Алерты на серию ошибок провайдера и на приближение к исчерпанию квоты. Логи — Serilog.

### 11.8 Объектное хранилище и async-задачи (C3)
- MinIO (S3-совместимое, `pgsty/minio`), бакет `finance-files`. **Бакет приватный** — наружу не публикуется.
- Импорт/экспорт — только асинхронно: `POST /import` и `POST /export` ставят Hangfire-задачу, возвращают `jobId` (202). Статус — `GET /jobs/{id}`. Состояние — сущность `BackgroundTask`.
- **Скачивание — только через ручку сервиса**, без presigned URL: `GET /jobs/{id}/result` стримит файл из MinIO через backend (`FileStreamResult`, без буферизации в память; ставит `Content-Disposition` / `Content-Type`). Поток всегда проходит через авторизацию приложения.
- **Авторизация и владение:** доступ только владельцу задачи — `BackgroundTask.UserId` через global query filter (admin — без ограничений). Даже при угадывании `id` чужой файл не отдаётся.
- **Неугадываемость файла (с учётом GUID v7):** PK всех сущностей — GUID v7 (§11.11), он **упорядочен по времени и потому перебираем** ⇒ `id` в URL не считается секретом. Защита от перебора обеспечивается тремя слоями: (1) проверка владельца (`BackgroundTask.UserId` через global query filter), (2) rate-limit на ручке, (3) **криптослучайный `ResultObjectKey`** (256 бит, base64url, `RandomNumberGenerator`) — opaque-ключ объекта в MinIO, не выводится из UserId/времени/последовательности и клиенту не раскрывается.
- **Rate-limit на `GET /jobs/{id}/result`** — отдельная политика (защита от массового скачивания / перебора). ⚠️ *Допущение: обрезанную фразу «rate-limit на э…» трактую как лимит на ручку скачивания; уточните, если имелся в виду другой эндпоинт.*

### 11.9 Резервное копирование (M4)
- Postgres: `pg_dump` по расписанию + Supabase PITR.
- MinIO: бэкап бакетов. Восстановление проверяется периодически.

### 11.10 На будущее: управление секретами (M7) ⚠️
- Сейчас секреты (`SUPABASE_JWT_SECRET`, токен ProverkaCheka, пароли БД/MinIO) хранятся в env / docker-compose `.env`. **Это сознательно отложено.**
- При выходе в прод — перенести в секрет-менеджер (Docker/Swarm secrets, HashiCorp Vault или аналог); токен провайдера держать строго server-side. Зафиксировано как технический долг.

### 11.11 Идентификаторы — GUID v7
- Все первичные ключи — **GUID v7** (`Guid.CreateVersion7()`, .NET 9+), генерация на стороне приложения. v7 упорядочен по времени → последовательные значения дают **локальность в B-tree индексе** (меньше расщеплений страниц, лучше кэш), в отличие от случайного v4.
- **Следствие для безопасности:** v7 содержит временну́ю метку и **перебираем** — `id` в URL не является секретом. Защита ресурсов (например, `GET /jobs/{id}/result`) строится на проверке владельца (global query filter) + rate-limit; неугадываемость файла обеспечивает отдельный криптослучайный `ResultObjectKey` (256 бит), а не сам id (см. §11.8).
