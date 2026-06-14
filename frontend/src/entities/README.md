# entities

Business entities and their domain models (FSD `entities` layer).

Each entity (e.g. `accrual`, `category`, `receipt`, `user`) gets its own slice with
`model/` (types, stores, query hooks), `api/` (request functions) and `ui/`
(entity-specific presentational components). Entities may depend only on `shared`.
