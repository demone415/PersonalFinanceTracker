# Finance Tracker

Full-stack приложение для учёта личных финансов со сканированием электронных чеков
(QR-код → ФНС / ProverkaCheka) и импортом выгрузки из «Налоги ФЛ» (`.xlsx`).

Подробное описание архитектуры — в [`ARCHITECTURE.md`](ARCHITECTURE.md),
требования — в [`Подготовка.md`](Подготовка.md), рабочие соглашения для разработки —
в [`CLAUDE.md`](CLAUDE.md).

---

## Возможности

- **Учёт операций (Accruals)** — доходы/расходы (`Income / ReturnIncome / Expense / ReturnExpense`),
  категории, группировка связанных операций (`GroupId`), мультивалютность с фиксацией курса на момент ввода.
- **Сканирование чеков** — QR-код чека отправляется на бэкенд, чек асинхронно дозагружается
  из ProverkaCheka через очередь (Hangfire FIFO) с расписанием повторов и дневным лимитом.
- **Импорт чеков из ФНС** — асинхронный импорт `.xlsx`-выгрузки «Налоги ФЛ» с дедупликацией.
- **Экспорт операций** — асинхронный экспорт в CSV.
- **Дашборд и аналитика** — сводки, расходы по категориям, динамика по месяцам
  (все агрегаты приводятся к базовой валюте пользователя).
- **Бюджеты** — месячные лимиты по категориям с отслеживанием прогресса.
- **Роли** — `user` / `admin`; изоляция данных по `UserId` через глобальные фильтры EF Core,
  админ видит данные всех пользователей.

---

## Технологический стек

### Backend (`backend/`)
- **.NET 10** — Clean Architecture + Feature Slices (без CQRS), решение в формате `.slnx`
- **ASP.NET Core Web API** — URL-versioning (`/api/v1/`), Scalar UI
- **EF Core + PostgreSQL** (миграции, глобальные query-фильтры, GUID v7, optimistic locking через `xmin`)
- **Hangfire** — фоновые задания (очередь чеков, импорт/экспорт) с хранением в PostgreSQL
- **Wolverine + RabbitMQ** — асинхронная шина сообщений (Outbox/Inbox)
- **FusionCache + Redis** — кэширование агрегатов, backplane, счётчики rate-limit
- **MinIO (S3)** — приватное объектное хранилище файлов (`finance-files`)
- **Refit + Polly** — HTTP-клиент к ProverkaCheka с retry/circuit-breaker
- **ClosedXML** — парсинг `.xlsx`-выгрузки ФНС
- **Serilog** — логирование; **OpenTelemetry → Prometheus** — метрики
- **xUnit + Moq + Testcontainers** — unit и integration тесты

### Frontend (`frontend/`)
- **React 19** + **TypeScript** + **Vite**
- **Feature Sliced Design** (`app / pages / widgets / features / entities / shared`)
- **TanStack Query v5** (server state) + **Zustand** (client state, тема, auth)
- **React Hook Form + Zod** — формы и валидация
- **Tailwind CSS v4** + **Radix UI** — UI-примитивы, тёмная тема по умолчанию
- **Recharts** — графики; **Motion** — анимации; **Lottie** — empty states
- **@supabase/supabase-js** — аутентификация через GoTrue
- **@undecaf/zbar-wasm** — распознавание QR-кодов в браузере

### Инфраструктура
- **Supabase GoTrue** — аутентификация (выпуск HS256 JWT), пользователи в `auth.users`
- **PostgreSQL** (образ `supabase/postgres` — включает схему `auth.*`)
- **Redis**, **RabbitMQ**, **MinIO**
- **Docker Compose** — оркестрация всего стека для локальной разработки
- **nginx** — раздача SPA и проксирование `/auth/v1/*` на GoTrue

---

## Требования

Для запуска через Docker:
- **Docker** и **Docker Compose** (v2)

Для локальной разработки без Docker:
- **.NET SDK 10**
- **Node.js 20+** и **npm**
- Запущенные PostgreSQL, Redis, RabbitMQ, MinIO, GoTrue (проще поднять через `docker compose`)

---

## Быстрый запуск (Docker)

```bash
# 1. Скопировать переменные окружения и при необходимости поправить
cp .env.example .env

# 2. Поднять весь стек (postgres → auth → backend [миграции] → db-seed → frontend)
docker compose up --build
```

После старта будут доступны:

| Сервис | URL |
|--------|-----|
| Frontend (SPA) | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| Scalar UI (API docs) | http://localhost:5000/scalar/v1 |
| Health / Metrics | http://localhost:5000/health/ready · `/metrics` |
| Supabase GoTrue (Auth) | http://localhost:9999/auth/v1/ |
| RabbitMQ Management (dev) | http://localhost:15672 |
| MinIO Console (dev) | http://localhost:9001 |

> Backend применяет EF-миграции при старте; сервис `db-seed` один раз (идемпотентно)
> заливает демо-данные.

### Демо-пользователи (создаются сидером)

Пароль у всех — `Password123!`:

| Email | Роль |
|-------|------|
| `user@example.com` | user |
| `family@example.com` | user |
| `admin@example.com` | admin |

> Публичная регистрация закрыта — новых пользователей создаёт администратор через GoTrue Admin API.

### Переменные окружения

Основное — в [`.env.example`](.env.example). Отдельно стоит отметить:

- `SUPABASE_JWT_SECRET` — общий HS256-секрет (≥32 символов), которым GoTrue подписывает,
  а .NET-бэкенд валидирует JWT. Для любого нелокального окружения замените на сильное значение.
- `RECEIPT_PROVIDER_TOKEN` — токен [ProverkaCheka](https://proverkacheka.com/). Без него сканирование чеков отключено. Можно получить зарегистрировавшись на сайте.
  (`GET /capabilities` → `receiptScanning: false`), но импорт `.xlsx` работает всегда.

---

## Локальная разработка

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/FinanceTracker.Api

# Миграции
dotnet ef migrations add <Name> --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api
dotnet ef database update  --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api

# Тесты
dotnet test
dotnet test --filter "FullyQualifiedName~CreateAccrual"   # один тест по имени
```

### Frontend

```bash
cd frontend
npm ci
npm run dev          # Vite dev server → http://localhost:5173
npm run build
npm run lint
npm run type-check
```

> При локальной разработке фронтенду нужны запущенные backend и GoTrue
> (укажите их адреса через `VITE_API_URL` / `VITE_SUPABASE_URL`).

---

## Структура репозитория

```
/
├── backend/              .NET 10 (Clean Architecture + Feature Slices)
│   ├── src/
│   │   ├── FinanceTracker.Api
│   │   ├── FinanceTracker.Application
│   │   ├── FinanceTracker.Domain
│   │   └── FinanceTracker.Infrastructure
│   ├── tests/            UnitTests + IntegrationTests
│   ├── db/seed.sql       демо-данные (Docker)
│   └── FinanceTracker.slnx
├── frontend/             React 19 SPA (Feature Sliced Design)
├── scripts/              вспомогательные скрипты (seed-users.ps1)
├── docker-compose.yml
├── .env.example
├── ARCHITECTURE.md       архитектурный план (источник истины)
└── CLAUDE.md             рабочие соглашения проекта
```

---

## Аутентификация (кратко)

Аутентификацией занимается **Supabase GoTrue**, а не .NET-бэкенд:

- Фронтенд логинится напрямую в GoTrue через `@supabase/supabase-js`.
- GoTrue выпускает HS256 JWT, подписанный `SUPABASE_JWT_SECRET`.
- .NET API валидирует JWT тем же секретом — без обращения к GoTrue на каждый запрос.
- Роль (`user` / `admin`) хранится в `app_metadata.role` и приходит в каждом JWT.

В .NET **нет** эндпоинтов login/register — они принадлежат GoTrue.
