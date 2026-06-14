Начало работы: вечер 2026-06-10

Использую Claude Code через Claude Desktop
Модель `Sonnet 4.6 / effort = High`, если не указано иное

Плагины: 
- [Karpathy-Inspired Claude Code Guidelines](https://github.com/multica-ai/andrej-karpathy-skills)
- [Everything Claude Code](https://github.com/affaan-m/ecc)
- [Anthropic skills](https://github.com/anthropics/skills)
- [Context7](https://github.com/upstash/context7)

MCP:
- Microsoft Learn
- Context7

Начал с дополнения требований, описания архитектуры проектов, правил, подхода к разработке, используемых технологий в файле `Подготовка.md`

Первые три вечера уделил архитектуре. Сначала расписал требования в "подготовке", потом попросил ИИ их агреггировать и дополнить. Потом с помощью более умной модели сделал ревью архитектуры, что оказалось очень полезным - она подсветила несколько проблем.

Я мало что понимаю во фронтенде и UI\UX. И не мог нормально сформировать требования по этой части. Поэтому посчитал необходимым сделать тестовый UI на моковых данных.

## Бэкенд: инициализация проекта (Story 1.1, таски T1.1.1–T1.1.6)

Начал разработку бэкенда. Ветка `feat/backend-init`.

- **T1.1.1** — solution в формате `.slnx`, централизованное управление пакетами
  (`Directory.Packages.props`) и общие свойства сборки (`Directory.Build.props`:
  `net10.0`, nullable, implicit usings, `TreatWarningsAsErrors`).
- **T1.1.2** — 4 проекта по Clean Architecture (Domain → Application → Infrastructure → Api)
  + 2 тестовых проекта (Unit/Integration, xUnit + Moq). Ссылки между слоями выставлены так,
  чтобы зависимости шли только внутрь.
- **T1.1.3** — EF Core 10 + Npgsql, `AppDbContext` (схема `public`, автоподхват конфигураций
  из сборки), `IUnitOfWork`/`UnitOfWork`, регистрация через `AddInfrastructure(IConfiguration)`.
- **T1.1.4** — Serilog: sinks и уровни вынесены в `appsettings.json` (в коде только
  `ReadFrom.Configuration`), console-sink, request logging. Проверил — структурированный
  вывод в консоль работает.
- **T1.1.5** — `Microsoft.AspNetCore.OpenApi` (документ `v1`) + Scalar UI, доступны только
  вне production. Проверил вручную: `/openapi/v1.json` отдаёт валидный OpenAPI 3.1.1,
  `/scalar/v1` — HTTP 200.
- **T1.1.6** — `docker-compose.yml` (postgres/redis/rabbitmq/backend/frontend) + многоступенчатый
  `backend/Dockerfile` + `.env.example`. `docker compose config` проходит. Сервис `frontend`
  пока под compose-профилем `frontend` (фронт появится в T1.1.7). Сборку образа отложил —
  Docker на машине пока не установлен; проверю после установки.

Версии пакетов выверял через `dotnet package search` под установленный SDK 10.0.109
(EF Core пришлось поднять до 10.0.9 — Npgsql 10.0.2 требует EF Core ≥ 10.0.4).
Полная сборка решения — `0 warnings, 0 errors`.

