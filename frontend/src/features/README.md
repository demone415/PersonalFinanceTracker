# features

User-facing features — a single user interaction that delivers value
(FSD `features` layer), e.g. `accrual-create`, `accrual-edit`, `scan-qr`,
`auth-login`.

Each feature slice owns its `model/`, `api/` and `ui/`. Features may depend on
`entities` and `shared`, never on other features, `widgets` or `pages`.
