# RaceDay API (Part 2)

A RESTful API built with ASP.NET Core Web API (.NET 8) and Entity Framework Core (Code-First, SQL Server).
It implements the endpoint plan from Part 1 (`API_Endpoint_Plan.md`).

![Build status](docs/green-build.png)

> Replace `docs/green-build.png` with your own screenshot of a successful (green) GitHub Actions run.

**Demo video (unlisted YouTube):** PASTE_YOUR_YOUTUBE_LINK_HERE

## The two roles

| Role | What they can do |
|---|---|
| **Organiser** | Create, update and delete their own events. Add, update and delete categories on their events. View everyone enrolled in their events. Capture and correct results. |
| **Participant** | Browse events and categories. Enrol in an event by choosing a category. View and cancel their own enrolments. View their own results. |

Both roles can register, log in and view/update their own profile. Browsing events, categories and event results is public.

How roles are managed: the role is chosen at registration, but it is validated against the fixed `Roles` table
(only `Organiser` and `Participant` exist) and is never accepted as a free ID. The role is then stored inside the
signed JWT, so the user cannot change it afterwards.

## Authentication and sessions

* `POST /api/auth/register` stores a **BCrypt hash** of the password (never the password itself).
* `POST /api/auth/login` returns a **JWT** containing the user's ID and role. This is the session: the client sends it as `Authorization: Bearer <token>` on every later request.
* Non-public endpoints return **401** without a valid token, and **403** if the role is wrong or the Organiser does not own the event.

## Setup and run

Requirements: Visual Studio 2022 (ASP.NET workload) or the .NET 8 SDK, and SQL Server / LocalDB.

1. Open `Part2/RaceDay.sln`.
2. Check the connection string in `RaceDay.Api/appsettings.json` (defaults to LocalDB, database `RaceDayDB`).
3. Create the database with a migration. In Visual Studio open **Tools > NuGet Package Manager > Package Manager Console**, set the default project to `RaceDay.Api` and run:
   ```
   Add-Migration InitialCreate
   Update-Database
   ```
   Or from a terminal in the `Part2` folder:
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project RaceDay.Api
   dotnet ef database update --project RaceDay.Api
   ```
4. Run `RaceDay.Api` (F5 or `dotnet run --project RaceDay.Api`). Swagger opens at `/swagger`.
5. In Swagger: register a user, log in, click **Authorize**, paste the token, then try the endpoints.

The `Roles` table is seeded automatically (Organiser, Participant).

## Run the tests

```
dotnet test Part2/RaceDay.sln
```

The tests start the real API in memory with an in-memory database, so they need no SQL Server.
They cover registration/login, event management, role-based rejection, enrolments and results,
each with success and failure cases.

## CI/CD

`.github/workflows/part2-ci.yml` restores, builds and runs all tests on every push.

## Project structure

```
Part2/
  RaceDay.Api/
    Controllers/   Auth, Users, Events, Categories, Enrolments, Results
    Models/        Entity classes (Role, User, Event, Category, Enrolment, Result)
    Data/          RaceDayDbContext (Code-First mapping + seeded roles)
    DTOs/          Request/response classes and EF projections
    Services/      JWT token creation
    Program.cs     Startup: EF Core, JWT auth, Swagger
  RaceDay.Tests/   xUnit tests
  Database/        Part2_Schema_Additions.sql
```

## Schema note (differences from Part 1)

The Part 2 brief says every event must capture a **distance** and an **event type** (run, walk or cycle).
The Part 1 `Events` table did not have these, so two columns were added: `EventType` and `DistanceKm`.
Everything else matches the Part 1 ERD. The matching SQL is in `Database/Part2_Schema_Additions.sql`.

## Endpoints

See Swagger for the live list. Summary (same as the Part 1 plan):

| Method | Route | Access |
|---|---|---|
| POST | /api/auth/register | Public |
| POST | /api/auth/login | Public |
| GET, PUT | /api/users/profile | Logged in |
| GET | /api/events, /api/events/{id}, /api/events/{id}/categories, /api/events/{id}/results | Public |
| POST, PUT, DELETE | /api/events, /api/events/{id} | Organiser (owner) |
| POST | /api/events/{id}/categories | Organiser (owner) |
| PUT, DELETE | /api/categories/{id} | Organiser (owner) |
| GET | /api/events/{id}/enrolments | Organiser (owner) |
| POST | /api/enrolments | Participant |
| GET | /api/enrolments/my | Participant |
| DELETE | /api/enrolments/{id} | Participant (owner) |
| POST, PUT | /api/results, /api/results/{id} | Organiser (owner) |
| GET | /api/results/my | Participant |
