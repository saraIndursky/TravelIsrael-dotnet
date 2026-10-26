# TravelIsrael — .NET Backend

API של **TravelIsrael** — פלטפורמה לגלישה, תכנון ויצירת טיולים בישראל.

> **Repository זה:** [TravelIsrael-dotnet](https://github.com/saraIndursky/TravelIsrael-dotnet)  
> **Frontend (React):** [TravelIsrael-react](https://github.com/saraIndursky/TravelIsrael-react)

**פרויקט לימודי** — נבנה בזוגות במסגרת קורס.

---

## ארכיטקטורה

```
┌─────────────────────┐       HTTPS / REST API       ┌─────────────────────┐
│   React Frontend    │  ◄────────────────────────►  │  .NET 8 Backend     │
│   TravelIsrael-react│       localhost:7081/api     │  (Repository זה)    │
└─────────────────────┘                              └──────────┬──────────┘
                                                                │
                                                                ▼
                                                     ┌─────────────────────┐
                                                     │     SQL Server      │
                                                     │   ProjectTripsDB    │
                                                     └─────────────────────┘
```

| שכבה | פרויקט | תיאור |
|------|--------|--------|
| **ProjectTrips** | Web API | Controllers, JWT, Swagger, CORS |
| **Service** | Business logic | DTOs, AutoMapper, TokenService |
| **Repository** | Data access | Entities, Repositories, IContext |
| **ProjectTripsDB** | EF Core | DbContext, Migrations |

---

## דרישות מקדימות

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server** / **LocalDB** — `(localdb)\MSSQLLocalDB`
- [TravelIsrael-react](https://github.com/saraIndursky/TravelIsrael-react) — לצד ה-Client (אופציונלי לבדיקות API)

---

## התקנה והרצה

### 1. Clone

```bash
git clone https://github.com/saraIndursky/TravelIsrael-dotnet.git
cd TravelIsrael-dotnet
```

### 2. מסד הנתונים (עם נתוני דוגמה)

**אפשרות א' — שחזור מגיבוי (מומלץ)**

קובץ הגיבוי נמצא ב-`Database/projectTripDB.bak` (כולל טיולים, מקומות, ביקורות ועוד).

1. פתחי **SSMS** והתחברי ל-`(localdb)\MSSQLLocalDB`
2. **Restore Database** מהקובץ `Database/projectTripDB.bak`
3. שם המסד: `ProjectTripsDB`

פירוט מלא: [Database/README.md](Database/README.md)

**אפשרות ב' — רק מבנה + Admin (בלי נתוני דוגמה)**

```bash
dotnet tool install --global dotnet-ef   # פעם ראשונה בלבד
dotnet ef database update --project ProjectTripsDB --startup-project ProjectTrips
```

Connection String מוגדר ב-`ProjectTripsDB/Models/ProjectTripsDataBase.cs`.  
אם SQL Server שלך רץ על שרת אחר — עדכני את השורה לפני שחזור/הרצת Migrations.

**משתמש Admin:**

| שדה | ערך |
|-----|-----|
| Email | `Admin@gmail.com` |
| Password | `Admin#613` |

### 3. הרצת השרת

```bash
dotnet run --project ProjectTrips
```

| כתובת | שימוש |
|-------|--------|
| `https://localhost:7081` | HTTPS (ברירת מחדל ל-Frontend) |
| `http://localhost:5188` | HTTP |
| `/swagger` | תיעוד API (Development) |

### 4. Build

```bash
dotnet build ProjectTrips.sln
```

---

## חיבור ל-Frontend

ב-React (`TravelIsrael-react`) כתובת ה-API מוגדרת ב-`src/services/axios.ts`:

```typescript
const baseURL = 'https://localhost:7081/api';
```

CORS מוגדר ב-`Program.cs` לאפשר `localhost:517x` (Vite).

---

## API Controllers

| Controller | Route | תיאור |
|------------|-------|--------|
| Auth | `api/Auth` | הרשמה, התחברות, Refresh Token |
| User | `api/User` | פרופיל, שינוי סיסמה |
| DayTrip | `api/DayTrip` | טיולים, סינון, המלצות, יצירה |
| Lookups | `api/Lookups` | אזורים, סוגים, קושי, נגישות |
| Suggestions | `api/Suggestions` | הצעות תחנות לבניית מסלול |
| Place | `api/Place` | מקומות |
| Route | `api/Route` | מסלולים |
| Admin | `api/Admin` | סטטיסטיקות וניהול |
| Review | `api/Review` | ביקורות |
| Enum | `api/enums` | ערכי Enum (קושי, נגישות, travel-mode) |

---

## מבנה הפרויקט

```
ProjectTrips/
├── ProjectTrips.sln
├── Database/               # גיבוי SQL (projectTripDB.bak)
├── ProjectTrips/           # ASP.NET Core Web API
│   ├── Controller/
│   ├── Program.cs
│   └── appsettings.json
├── Service/                # שירותים + DTOs
├── Repository/             # Entities + Repositories
└── ProjectTripsDB/         # EF Core + Migrations
```

---

## JWT

הגדרות ב-`ProjectTrips/appsettings.json`. המפתח (`Jwt:Key`) לא נשמר בקוד, אלא ב-User Secrets.
לפני ההרצה הראשונה מגדירים מפתח באורך 32 תווים לפחות:

```bash
dotnet user-secrets set "Jwt:Key" "<מפתח אקראי באורך 32 תווים לפחות>" --project ProjectTrips
```

```json
"Jwt": {
  "Key": "...",
  "Issuer": "ProjectTrips",
  "Audience": "ProjectTrips",
  "ExpireMinutes": 30
}
```

---

## תמיכה

לשאלות או באגים — פתחי **Issue** ב-GitHub.
