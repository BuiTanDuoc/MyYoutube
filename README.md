# MiniTube – Backend (Step 1: Identity / User management)

.NET 8 · Clean Architecture · EF Core + SQL Server · ASP.NET Core Identity · JWT + rotating refresh tokens

## Setup

```bash
cd MiniTube
dotnet new sln -n MiniTube
dotnet sln add src/MiniTube.Domain src/MiniTube.Application src/MiniTube.Infrastructure src/MiniTube.Api

# JWT secret (don't keep real secrets in appsettings.json)
cd src/MiniTube.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# First migration (run from the solution root)
cd ../..
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add InitialIdentity -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations

dotnet run --project src/MiniTube.Api    # Development: auto-migrates + seeds roles & admin
```

Swagger: https://localhost:7100/swagger
Seeded admin (Development only): `admin` / `Admin@12345` – change it.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | /api/auth/register | – |
| POST | /api/auth/login | – |
| POST | /api/auth/refresh | – |
| POST | /api/auth/logout | – |
| POST | /api/auth/logout-all | Bearer |
| GET / PUT | /api/users/me | Bearer |
| POST | /api/users/me/change-password | Bearer |
| GET | /api/users/{id} | – (public profile) |
| GET | /api/admin/users | Admin |
| PUT | /api/admin/users/{id}/lock | Admin |
| PUT | /api/admin/users/{id}/roles | Admin |
