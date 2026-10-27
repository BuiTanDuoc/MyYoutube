# MiniTube – Backend (Identity / User management)

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

# Migration (run from the solution root)
cd ../..
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add InitialIdentity -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations
# Already applied an earlier InitialIdentity? Add a second one instead (only adds the DataProtectionKeys table):
#   dotnet ef migrations add AddDataProtectionKeys -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations

dotnet run --project src/MiniTube.Api    # Development: auto-migrates + seeds roles & admin
```

Swagger: https://localhost:7100/swagger
Seeded admin (Development only): `admin` / `Admin@12345` – change it.

## Email

- No `Email:Host` configured -> emails are **printed in the console log** (copy the link from there).
- Real SMTP, e.g. Brevo: `Email:Host=smtp-relay.brevo.com`, `Port=587`, `UserName`, `Password` (use user-secrets), `FromAddress` = a verified sender.
- Links point to `Frontend:BaseUrl` -> `/confirm-email?userId=..&token=..` and `/reset-password?email=..&token=..`
- Email language follows the `Accept-Language` header (`en...` = English, anything else = Vietnamese).
  Make the React axios client send the language selected in the app.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | /api/auth/register | – |
| POST | /api/auth/login | – |
| POST | /api/auth/refresh | – |
| POST | /api/auth/logout | – |
| POST | /api/auth/logout-all | Bearer |
| POST | /api/auth/confirm-email | – |
| POST | /api/auth/resend-confirmation | – (5 / 10 min / IP) |
| POST | /api/auth/forgot-password | – (5 / 10 min / IP) |
| POST | /api/auth/reset-password | – |
| GET / PUT | /api/users/me | Bearer |
| PUT / DELETE | /api/users/me/avatar | Bearer (multipart `file`) |
| POST | /api/users/me/change-password | Bearer |
| GET | /api/users/{id} | – (public profile) |
| GET | /api/admin/users | Admin |
| PUT | /api/admin/users/{id}/lock | Admin |
| PUT | /api/admin/users/{id}/roles | Admin |

Error responses are ProblemDetails; some include `"code"` (`invalid_token`, `account_locked`) for the frontend to switch on.

## Notes

- Unconfirmed users can log in, but endpoints guarded by `[Authorize(Policy = Policies.VerifiedEmail)]` (use it for video upload) need `email_verified=true` in the access token -> after confirming, the client calls `/api/auth/refresh`.
- Avatars: JPEG/PNG/WebP up to 2 MB, decoded and re-encoded to 256x256 WebP (EXIF stripped), saved under `Storage:RootPath` and served from `/media/avatars/...`. `avatarUrl` in responses is relative: prefix it with the API base URL.
- ImageSharp has a split license (free for personal/open-source use and small companies); check it if you ever monetize.
