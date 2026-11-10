# Safqa (صفقة) CRM

ASP.NET Core 8 · EF Core + PostgreSQL · React 18 + Vite + Tailwind · SignalR · QuestPDF · Arabic (RTL) / English (LTR), light / dark.

## Demo accounts (seeded automatically in Development, or whenever `Seed__Demo=true`)

| Role (Identity role) | Email | Password |
|---|---|---|
| Admin (`Admin`) | admin@safqa.com | `Safqa@Admin2026!` |
| Manager (`SalesManager`) | manager@safqa.com | `Safqa@Manager2026!` |
| Rep 1 (`SalesRep`) | ahmed@safqa.com | `Safqa@Rep2026!` |
| Rep 2 (`SalesRep`) | sarah@safqa.com | `Safqa@Rep2026!` |

Demo data: 5 leads (New / Contacted / Qualified / Converted), 3 companies + contacts, 4 deals across stages (one stalled, one won), 4 products,
2 quotes (the second exceeds the rep's 10 % discount limit → *Pending approval*), tasks and meetings for the dashboard.
**Never run with demo seeding in production** – these passwords are public. Use `Seed__Demo=false` and `SEED_ADMIN_PASSWORD` instead.

## Prerequisites (Windows, macOS, Linux)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js 20+](https://nodejs.org), [Docker Desktop / Engine](https://docs.docker.com/get-docker/)
- Windows: use PowerShell (`$env:NAME="x"` instead of `export`). macOS/Linux: any shell.

## A) Local development (DB in Docker, API + web on your machine)
```bash
docker compose up -d db                       # Postgres 16 with healthcheck + persistent volume
dotnet run --project src/Safqa.API            # http://localhost:5000  (Development: dev JWT key + demo seed are built in)
cd frontend && npm install && npm run dev     # http://localhost:5173  (proxies /api and /hubs to :5000)
```
Swagger: http://localhost:5000/swagger. To point the web app at another API: `VITE_API_TARGET=http://host:port npm run dev`.

## B) Full stack in containers
```bash
cp .env.example .env                          # Windows: copy .env.example .env
docker compose --profile full up -d --build
```
Web http://localhost:3000 · API http://localhost:5000 · DB localhost:5432. Stop: `docker compose --profile full down` (add `-v` to wipe data).

## Database & migrations
On start the API applies EF migrations if the project has any, otherwise it creates the schema directly (`EnsureCreated`) – fine for demos.
For real deployments create and commit migrations first:
```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial -p src/Safqa.Infrastructure -s src/Safqa.API
dotnet ef database update            -p src/Safqa.Infrastructure -s src/Safqa.API
```
(Don't mix the two modes on the same database: if you started with `EnsureCreated`, drop the DB before switching to migrations.)

## Tests
```bash
dotnet test                                   # xUnit + FluentAssertions + EF InMemory
cd frontend && npm run build                  # type-check (tsc --noEmit) + production build
```
Covers round-robin routing (even spread, inactive/manager exclusion), lead conversion (atomic create, status rules, duplicate guard),
deal stage moves (409 on stale/concurrent version), and quote maths (discount, 14 %/15 % VAT, rounding).

## Useful endpoints
`POST /api/v1/auth/login` · `GET /api/v1/deals/board` · `POST /api/v1/deals/{id}/move-stage` · `GET /api/v1/deals/{id}/activities` ·
`PUT /api/v1/leads/{id}/status` · `POST /api/v1/leads/{id}/convert` (qualified leads only) · `GET /api/v1/leads/export` and `/deals/export` (manager/admin, CSV) ·
`GET /api/v1/quotes/{id}/pdf?lang=ar|en`. Arabic PDFs need the *Noto Sans Arabic* font on the server (included in the Docker image).
