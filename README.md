# RaceDay

A full-stack event management system for the South African road running, walking, and cycling community. RaceDay lets Event Organisers create and manage events, categories, and participant results, while Participants can browse events, enter categories, and track their own results.

This repository contains the Portfolio of Evidence (PoE) for RaceDay, submitted in three parts:
- **Part 1:** System planning — ERD, API endpoint plan, and SQL database script.
- **Part 2:** RESTful API built in C#, connected to the database, with unit tests and GitHub CI/CD.
- **Part 3:** MVC web application consuming the API, with Azure Blob Storage and Docker.

---

## System Description

South Africa hosts hundreds of road running, walking, and cycling events every weekend, from major events like the Comrades Marathon and the Cape Town Cycle Tour down to local park runs and charity rides. Many of these are still managed with paper registration and spreadsheets. RaceDay digitises this process: Organisers can create events with multiple categories (e.g. 5km, 10km), Participants can register and enter events online, and results are captured and tracked in one place.

## User Roles

RaceDay supports two distinct user roles:

- **Organiser** — can create, edit, and delete events; manage event categories; capture participant results; and view all enrolments for events they own.
- **Participant** — can create an account, browse events, enter an event by selecting a category, view their own enrolments, and track their personal results.

Role-based access is enforced at the API level (Part 2) and reflected consistently in the MVC interface (Part 3).

---

## Repository Structure

```
/docs
  ├── RaceDay_ERD.png                 # Entity Relationship Diagram
  ├── API_Endpoint_Plan.md            # Full API endpoint plan
  └── RaceDay_Database.sql            # SQL database creation & seed script
/.github
  └── workflows
      └── dotnet-ci.yml               # CI/CD build & test workflow
README.md
```

---

## Setup Instructions

### Prerequisites
- SQL Server / SQL Server Management Studio (SSMS)
- .NET 8 SDK (for Part 2 onward)
- Git

### Database Setup
1. Open SQL Server Management Studio (SSMS).
2. Open `/docs/RaceDay_Database.sql`.
3. Execute the script against your SQL Server instance. This creates the `RaceDayDB` database, all six tables, and seeds sample data (2 Organisers, 2 Participants, 3 Events, categories, and enrolments).
4. Verify the tables loaded correctly by running the verification queries commented at the bottom of the script.

### Clone the Repository
```
git clone <your-repo-url>
cd RaceDay
```

---

## Planning Documents (Part 1)

- **ERD:** [`/docs/RaceDay_ERD.png`](./docs/RaceDay_ERD.png) — six entities (Roles, Users, Events, Categories, Enrolments, Results) with primary keys, foreign keys, and cardinality shown for every relationship.
- **API Endpoint Plan:** [`/docs/API_Endpoint_Plan.md`](./docs/API_Endpoint_Plan.md) — every planned endpoint covering Authentication, User Profile, Events, Categories, Enrolments, and Results.
- **SQL Script:** [`/docs/RaceDay_Database.sql`](./docs/RaceDay_Database.sql) — matches the ERD exactly.

---

## CI/CD

This repository uses GitHub Actions for continuous integration. The workflow at `.github/workflows/dotnet-ci.yml` runs on every push and pull request to `main`, restoring dependencies, building the solution, and running unit tests.

**Successful build screenshot:**

> _[Insert screenshot of a green/successful GitHub Actions run here]_

---

## Video Walkthrough

An unlisted YouTube video walking through the planning documents, ERD decisions, endpoint plan choices, and a live run of the SQL script in SSMS:

> https://youtu.be/H3CplLZSQmk
---

## AI Tool Disclosure

AI tools were used during the planning process to assist with drafting the ERD structure, the API endpoint plan, and the SQL script template. All output was reviewed, adapted, and verified against the assignment requirements before submission.
