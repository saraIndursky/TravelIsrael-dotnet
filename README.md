# TravelIsrael — .NET Backend

The API of **TravelIsrael**, a platform for browsing, planning and creating day trips in Israel.

> **This repository:** [TravelIsrael-dotnet](https://github.com/saraIndursky/TravelIsrael-dotnet)  
> **Frontend (React):** [TravelIsrael-react](https://github.com/saraIndursky/TravelIsrael-react)

**Course project**, built by a team of two.

---

## Architecture

```
┌─────────────────────┐       HTTPS / REST API       ┌─────────────────────┐
│   React Frontend    │  ◄────────────────────────►  │  .NET 8 Backend     │
│   TravelIsrael-react│       localhost:7081/api     │  (this repository)  │
└─────────────────────┘                              └──────────┬──────────┘
                                                                │
                                                                ▼
                                                     ┌─────────────────────┐
                                                     │     SQL Server      │
                                                     │   ProjectTripsDB    │
                                                     └─────────────────────┘
```

| Layer | Project | Description |
|------|--------|--------|
| **ProjectTrips** | Web API | Controllers, JWT, Swagger, CORS |
| **Service** | Business logic | DTOs, AutoMapper, TokenService |
| **Repository** | Data access | Entities, Repositories, IContext |
| **ProjectTripsDB** | EF Core | DbContext, Migrations |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server** / **LocalDB** — `(localdb)\MSSQLLocalDB`
- [TravelIsrael-react](https://github.com/saraIndursky/TravelIsrael-react) for the client (optional for API testing)

---

## Setup and running

### 1. Clone

```bash
git clone https://github.com/saraIndursky/TravelIsrael-dotnet.git
cd TravelIsrael-dotnet
```

### 2. Database (with sample data)

**Option A — restore from backup (recommended)**

The backup file is `Database/projectTripDB.bak` (includes trips, places, reviews and more).

1. Open **SSMS** and connect to `(localdb)\MSSQLLocalDB`
2. **Restore Database** from `Database/projectTripDB.bak`
3. Database name: `ProjectTripsDB`

Full details: [Database/README.md](Database/README.md)

**Option B — schema and admin user only (no sample data)**

```bash
dotnet tool install --global dotnet-ef   # first time only
dotnet ef database update --project ProjectTripsDB --startup-project ProjectTrips
```

The connection string is set in `ProjectTripsDB/Models/ProjectTripsDataBase.cs`.
If your SQL Server runs elsewhere, update it before restoring or running migrations.

**Admin user:**

| Field | Value |
|-----|-----|
| Email | `Admin@gmail.com` |
| Password | `Admin#613` |

### 3. JWT key

The signing key (`Jwt:Key`) is not stored in the code. Before the first run, set a key of at
least 32 characters with User Secrets:

```bash
dotnet user-secrets set "Jwt:Key" "<random key, at least 32 characters>" --project ProjectTrips
```

The other JWT settings are in `ProjectTrips/appsettings.json`:

```json
"Jwt": {
  "Key": "",
  "Issuer": "ProjectTrips",
  "Audience": "ProjectTrips",
  "ExpireMinutes": 30
}
```

### 4. Run the server

```bash
dotnet run --project ProjectTrips
```

| Address | Use |
|-------|--------|
| `https://localhost:7081` | HTTPS (default for the frontend) |
| `http://localhost:5188` | HTTP |
| `/swagger` | API documentation (development) |

### 5. Build

```bash
dotnet build ProjectTrips.sln
```

---

## Frontend connection

In the React app (`TravelIsrael-react`), the API address is set in `src/services/axios.ts`:

```typescript
const baseURL = 'https://localhost:7081/api';
```

CORS in `Program.cs` allows `localhost:517x` (Vite).

---

## API controllers

| Controller | Route | Description |
|------------|-------|--------|
| Auth | `api/Auth` | Sign-up, login, refresh token |
| User | `api/User` | Profile, password change |
| DayTrip | `api/DayTrip` | Trips, filtering, recommendations, creation |
| Lookups | `api/Lookups` | Regions, types, difficulty, accessibility |
| Suggestions | `api/Suggestions` | Stop suggestions for the trip builder |
| Place | `api/Place` | Places |
| Route | `api/Route` | Routes |
| Admin | `api/Admin` | Statistics and management |
| Review | `api/Review` | Reviews |
| Enum | `api/enums` | Enum values (difficulty, accessibility, travel mode) |

---

## Project structure

```
├── ProjectTrips.sln
├── Database/               # SQL Server backup (projectTripDB.bak)
├── ProjectTrips/           # ASP.NET Core Web API
│   ├── Controller/
│   ├── Program.cs
│   └── appsettings.json
├── Service/                # Services and DTOs
├── Repository/             # Entities and repositories
└── ProjectTripsDB/         # EF Core and migrations
```

---

## Feedback

For questions or bugs, open an **issue** on GitHub.
