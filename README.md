# LogiTrack

An ASP.NET Core 9 Web API for tracking warehouse inventory and customer orders. It uses JWT authentication with role-based access, and stores data in SQLite through Entity Framework Core.

## Features

- **Inventory and orders.** You can create, list, and delete inventory items. An order is a group of items placed by a user.
- **JWT authentication.** ASP.NET Core Identity stores the users. Login returns a signed token that lasts 60 minutes.
- **Roles.** New accounts get the `User` role. An `Admin` can delete items and orders, see every order, and promote other users.
- **Order ownership.** Users can only see their own orders. Other users' orders return `404`.
- **Account protection.** Passwords must be at least 12 characters. An account locks for 15 minutes after 5 failed logins. Register and login are rate-limited to 10 requests per minute per IP.
- **Caching.** Inventory pages are cached in memory. Every write that changes inventory clears the cache.
- **Paging.** List endpoints accept `page` and `pageSize` and report the total in an `X-Total-Count` header.
- **Operations.** There is a `/health` endpoint, an OpenAPI document, and errors are returned as `ProblemDetails`.

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- The EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Configure secrets

Secrets are kept out of the repository. In development, store them with [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
cd LogiTrack
dotnet user-secrets set "Jwt:Key" "<random string, at least 32 characters>"

# Optional: creates an Admin account on startup if it doesn't already exist
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Email" "admin@logitrack.local"
dotnet user-secrets set "SeedAdmin:Password" "<at least 12 characters>"
```

One way to generate a key is `openssl rand -base64 48`. The app won't start without `Jwt:Key`.

### 2. Create the database

```bash
dotnet ef database update
```

This creates `logitrack.db` next to the project. The database file is git-ignored.

### 3. Run

```bash
dotnet run --launch-profile http
```

The API listens on `http://localhost:5247`. The `https` profile also listens on `https://localhost:7138`.

To try the API, open `LogiTrack/LogiTrack.http` in Rider or Visual Studio. Its login request stores the token, and every other request in the file sends it.

## Authentication

1. Register:

   ```http
   POST /api/Auth/register
   Content-Type: application/json

   { "userName": "jane", "email": "jane@example.com", "password": "a-long-password-1", "firstName": "Jane", "lastName": "Doe" }
   ```

2. Log in to get a token:

   ```http
   POST /api/Auth/login
   Content-Type: application/json

   { "userName": "jane", "password": "a-long-password-1" }
   ```

   ```json
   { "token": "eyJhbGciOi...", "expires": "2026-10-20T13:00:00Z" }
   ```

3. Send the token on every other request:

   ```http
   Authorization: Bearer eyJhbGciOi...
   ```

The token carries the user's roles. When an Admin promotes a user, the new role takes effect at the user's next login.

## API reference

| Method | Route | Access | Description |
|---|---|---|---|
| `POST` | `/api/Auth/register` | Anonymous, rate-limited | Create an account with the `User` role |
| `POST` | `/api/Auth/login` | Anonymous, rate-limited | Exchange username and password for a JWT |
| `POST` | `/api/Auth/promote/{userName}` | Admin | Grant a user the `Admin` role |
| `GET` | `/api/Inventory?page=1&pageSize=50` | Authenticated | List inventory items (cached) |
| `POST` | `/api/Inventory` | Authenticated | Add an item: `{ "name", "quantity" (≥ 1), "location" }` |
| `DELETE` | `/api/Inventory/{id}` | Admin | Delete an item |
| `GET` | `/api/orders?page=1&pageSize=50` | Authenticated | List orders (your own; Admins see all) |
| `GET` | `/api/orders/{id}` | Owner or Admin | Get an order and its items |
| `POST` | `/api/orders` | Authenticated | Place an order: a non-empty array of items |
| `DELETE` | `/api/orders/{id}` | Admin | Delete an order and its items |
| `GET` | `/health` | Anonymous | Health check, including database connectivity |
| `GET` | `/openapi/v1.json` | Anonymous, Development only | OpenAPI document |

**Paging:** `page` starts at 1. `pageSize` defaults to 50, and the maximum is 100. Responses are plain JSON arrays, and the total number of rows is in the `X-Total-Count` response header.

**Errors:** Errors are [RFC 9110 `ProblemDetails`](https://www.rfc-editor.org/rfc/rfc9110) objects:
- `400` for validation failures
- `401` for a missing or invalid token
- `403` when your role isn't allowed
- `404` when a resource is missing or isn't yours
- `429` when you hit the rate limit

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `ConnectionStrings:LogiTrack` | `Data Source=logitrack.db` | SQLite connection string |
| `Jwt:Key` | *(required, secret)* | HMAC-SHA256 signing key, at least 32 bytes |
| `Jwt:Issuer` / `Jwt:Audience` | `LogiTrack` | Issuer and audience the token must match |
| `Jwt:ExpiryMinutes` | `60` | Token lifetime |
| `SeedAdmin:UserName` / `Email` / `Password` | *(unset)* | Initial Admin account created at startup |
| `RateLimiting:AuthPermitLimit` | `10` | Register/login requests allowed per IP per minute |

In production, supply secrets as environment variables. Use a double underscore where the key has a colon, for example `Jwt__Key` or `SeedAdmin__Password`. You can also use a secret store. Don't put secrets in `appsettings.*.json`.

## Testing

```bash
dotnet test
```

`LogiTrack.Tests` contains integration tests. They run the full app through `WebApplicationFactory` against a temporary SQLite database built from the real migrations. The tests cover:
- authentication and roles
- account lockout and password rules
- input validation
- paging
- cache invalidation
- order ownership

Tests don't need user-secrets because the test factory supplies its own configuration.

GitHub Actions (`.github/workflows/ci.yml`) builds the solution and runs the tests on every push to `main` and on every pull request.

## Database migrations

After changing an entity or `LogiTrackContext`, add and apply a migration:

```bash
cd LogiTrack
dotnet ef migrations add <Name>
dotnet ef database update
```

## Project structure

```
LogiTrack/
├── Controllers/         Auth, Inventory and Order endpoints
├── Data/                LogiTrackContext (EF Core + Identity)
├── Models/              Entities (InventoryItem, Order, ApplicationUser) and request/response DTOs
├── Migrations/          EF Core migrations
├── Util/                Role constants and seeding, paging constants, cache invalidation signal
├── Program.cs           Service registration and middleware pipeline
└── LogiTrack.http       Ready-made requests for the IDE HTTP client
LogiTrack.Tests/         Integration tests
```

## Deploying to production

- **Behind a reverse proxy,** add the proxy's address to `ForwardedHeadersOptions.KnownProxies` in `Program.cs`. Otherwise the rate limiter sees every client as the proxy's IP.
- **Use a server database for multiple instances.** SQLite and the in-memory cache only work for a single instance. With multiple instances, move to PostgreSQL or SQL Server and a distributed cache.
- **Apply migrations as a deployment step,** for example with `dotnet ef migrations bundle`. The app does not migrate the database on startup.
