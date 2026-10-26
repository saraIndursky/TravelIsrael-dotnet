# Database backup

**`projectTripDB.bak`** is a SQL Server backup of `ProjectTripsDB` with all the sample data (trips, places, reviews, etc.).

## Restore with SSMS (recommended)

1. Open **SQL Server Management Studio**
2. Connect to `(localdb)\MSSQLLocalDB`
3. If a database named `ProjectTripsDB` already exists, delete it (right-click → Delete)
4. Right-click **Databases** → **Restore Database...**
5. **Device** → **Add** → select `Database/projectTripDB.bak` from the repository
6. **Database** → name: `ProjectTripsDB`
7. **Options** → check **Overwrite the existing database**
8. **OK**

## Restore with T-SQL

Replace the path with the full path of the file on your machine:

```sql
RESTORE DATABASE ProjectTripsDB
FROM DISK = N'C:\path\to\TravelIsrael-dotnet\Database\projectTripDB.bak'
WITH REPLACE,
MOVE N'ProjectTripsDB' TO N'C:\Users\<USER>\ProjectTripsDB.mdf',
MOVE N'ProjectTripsDB_log' TO N'C:\Users\<USER>\ProjectTripsDB_log.ldf';
```

> If the logical file names are different, run this first:
> `RESTORE FILELISTONLY FROM DISK = N'...\projectTripDB.bak'`

## After restoring

```bash
dotnet run --project ProjectTrips
```

There is **no** need to run `dotnet ef database update`; the backup already includes all tables and data.

**Admin (if present in the backup):**

| Email | Password |
|-------|----------|
| `Admin@gmail.com` | `Admin#613` |
