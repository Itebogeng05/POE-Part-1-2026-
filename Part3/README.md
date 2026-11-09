# RaceDay (Part 3): MVC Web Application, Azure Blob Storage and Docker

RaceDay is a platform where **Organisers** publish running, walking and cycling events and **Participants** find events, enrol and see their results.

| Part | What it is |
|---|---|
| Part 2 | `RaceDay.Api`: ASP.NET Core Web API (.NET 8), EF Core Code-First, SQL Server, JWT |
| **Part 3** | `RaceDay.Web`: ASP.NET Core **MVC** front end that consumes the API, **Azure Blob Storage** for banner images (added to the API), and **Docker** |

![Build status](docs/green-build.png)

> Replace `docs/green-build.png` with a screenshot of your green GitHub Actions run.

**Demo video (unlisted YouTube):** PASTE_YOUR_YOUTUBE_LINK_HERE

## Architecture

```
Browser  ->  RaceDay.Web (MVC, session)  ->  RaceDay.Api (REST, JWT)  ->  SQL Server
                                                      |
                                                      +-> Azure Blob Storage (event banners)
```

* The MVC project has **no database access and no business logic**. Every read and write is an HTTP call to the API (`Services/RaceDayApi.cs`).
* The MVC project **never talks to Azure**. Banner images are uploaded to `POST /api/events/{id}/banner`; the API stores them in a private blob container. The browser loads banners from the MVC route `/Events/{id}/Banner`, which fetches them from the API.
* Authentication: the user registers/logs in through the MVC app, which calls `POST /api/auth/login`. The returned user ID, role, name and JWT are kept in **server-side session** state. Every page checks the session (`Filters/SessionAuthorizeAttribute.cs`).

## Features by role

**Organiser**
* Dashboard: events created, total enrolments per event, upcoming dates
* Events: create, edit, delete (name, description, date, location, distance, event type) with a **banner image upload**
* Categories: add, edit and delete age/distance categories per event
* Enrolments: everyone enrolled in an event, with category and status
* Results: capture and publish finish time and finishing position after the event has taken place

**Participant**
* Home: upcoming events with filters (search, type, date range, distance)
* Event page: full details, banner, categories and the enrol form
* My Enrolments: events entered, colour-coded status (green = Confirmed, amber = Pending), cancel
* My Results: race history with event, date, category, finish time and position (e.g. "47th out of 312 finishers")

Unauthenticated users are redirected to Login for every protected page; the wrong role is sent to an Access Denied page; Log out clears the session.

## Run it

### Option A: Docker (MVC app in a container)

The MVC container needs a running RaceDay API (Part 2 with the Part 3 additions). Start the API first (see Option B, step 2), then:

```bash
docker build -t raceday-web RaceDay.Web && docker run --rm -p 8080:8080 --add-host=host.docker.internal:host-gateway -e ApiSettings__BaseUrl=http://host.docker.internal:5100/ raceday-web
```

Open <http://localhost:8080>.

* `ApiSettings__BaseUrl` is the address of the API **as seen from inside the container**. `host.docker.internal` is the machine running Docker. To use a deployed API, put its URL there instead.
* `--add-host` is needed on Linux; Docker Desktop (Windows/macOS) already provides the name and ignores it.

### Option B: Run locally (Visual Studio or terminal)

1. Install the .NET 8 SDK and SQL Server LocalDB (or SQL Server).
2. Start the API. The first start applies the EF migrations (including `AddEventBanner`).
   ```bash
   dotnet run --project RaceDay.Api --launch-profile RaceDay.Api
   ```
   The API listens on `http://localhost:5100` (Swagger at `/swagger`).
3. Start the MVC app in a second terminal:
   ```bash
   dotnet run --project RaceDay.Web
   ```
   Open <http://localhost:5200>. The API address is `ApiSettings:BaseUrl` in `RaceDay.Web/appsettings.json`.

In Visual Studio, set **Multiple startup projects** (`RaceDay.Api` and `RaceDay.Web`).

### Option C: Whole system in one command (optional)

`docker-compose.yml` starts SQL Server, the Azurite blob emulator, the API and the MVC app together:

```bash
docker compose up --build
```

Open <http://localhost:8080>. The API is also on <http://localhost:5100/swagger>.

## Azure Blob Storage

Configured in `RaceDay.Api/appsettings.json` (the API only):

```json
"AzureStorage": { "ConnectionString": "UseDevelopmentStorage=true", "ContainerName": "event-banners" }
```

* Local development: install [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) and run `azurite-blob`, or use the compose file above.
* Real Azure: create a Storage Account and set the connection string with an environment variable or user secrets. **Never commit a real key.**
  ```bash
  dotnet user-secrets set "AzureStorage:ConnectionString" "<your connection string>" --project RaceDay.Api
  ```
* The container is created automatically as **private**; images are only served through the API.
* Upload rules (checked in the MVC form and again in the API): JPG, PNG, WEBP or GIF, max 5 MB, and the file contents must really be an image. Replacing a banner or deleting an event removes the old blob.

## Docker notes

`RaceDay.Web/Dockerfile` is a two-stage build:
* **Build stage** (`dotnet/sdk:8.0`): copies the `.csproj` first so the package restore layer is cached, then publishes a Release build.
* **Runtime stage** (`dotnet/aspnet:8.0`): only the published output, runs as the **non-root** `app` user and listens on port 8080.
* A `.dockerignore` keeps `bin/`, `obj/` and IDE files out of the build context.
* Configuration is by environment variable (`ApiSettings__BaseUrl`), so the same image works everywhere. Culture is fixed to `en-US` so numbers and dates behave identically in Docker and locally.

## Tests

```bash
dotnet test RaceDay.sln
```

The API tests (xUnit) run the real API in memory with an in-memory database and a **fake blob store**, so they need no SQL Server or Azure. They include `BannerTests` for the Part 3 upload/download rules (owner only, file type, real image check, replacing and deleting).

## CI/CD (GitHub Actions)

`.github/workflows/part3-ci.yml` (at the repository root) runs on every push and pull request:
1. restore, build the **API**
2. build the **MVC project**
3. run all tests
4. build the MVC **Docker image**

## Project structure

```
Part3/
  RaceDay.Api/            Part 2 API + Azure Blob Storage (Services/BlobStorageService.cs, banner endpoints)
  RaceDay.Web/            ASP.NET Core MVC front end
    Controllers/          Account, Home, Events, Participant, Organiser* controllers
    Filters/              SessionAuthorizeAttribute (session + role check)
    Models/               API DTOs, view models, UiFormat (consistent formatting)
    Services/             RaceDayApi (the only data source), ApiResult, SessionUser
    Views/                Razor views, one shared layout with a role-aware menu
    wwwroot/              site.css (RaceDay theme), site.js
    Dockerfile
  RaceDay.Tests/          xUnit tests
  Database/               SQL for the schema additions
  docker-compose.yml      Optional full-system run
```

## Design

Bootstrap 5 for layout and responsiveness, with a custom sporting theme (navy, energetic orange, condensed headings) defined once in `wwwroot/css/site.css` and used on every page. Invalid input shows friendly field messages; API or network failures show a plain-language notice instead of an error page.
