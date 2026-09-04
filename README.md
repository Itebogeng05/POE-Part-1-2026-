# RaceDay – Part 1: System Planning and Database

A full-stack event management system for the South African road running, walking, and cycling community. RaceDay lets Event Organisers create and manage events, categories, and participant results, while Participants can browse events, enter categories, and track their own results.

This repository contains the Portfolio of Evidence (PoE) for RaceDay. **This README currently covers Part 1 (System Planning and Database).** Part 2 (RESTful API) and Part 3 (MVC application) will extend this repository and this README as they are completed.

---

## System Description

South Africa hosts hundreds of road running, walking, and cycling events every weekend, from major events like the Comrades Marathon and the Cape Town Cycle Tour down to local park runs and charity rides. Many of these are still managed with paper registration and spreadsheets. RaceDay digitises this process: Organisers can create events with multiple categories (e.g. 5km, 10km), Participants can register and enter events online, and results are captured and tracked in one place.

## User Roles

RaceDay supports two distinct user roles:

- **Organiser** — can create, edit, and delete events; manage event categories; capture participant results; and view all enrolments for events they own.
- **Participant** — can create an account, browse events, enter an event by selecting a category, view their own enrolments, and track their personal results.

Role-based access will be enforced at the API level in Part 2 and reflected consistently in the MVC interface in Part 3.

---

## Repository Structure (Part 1)

```
/docs
  ├── RaceDay_ERD.png                 # Entity Relationship Diagram
  ├── API_Endpoint_Plan.md            # Full API endpoint plan
  └── RaceDay_Database.sql            # SQL database creation & seed script
/.github
  └── workflows
      └── dotnet-ci.yml               # CI/CD workflow
README.md
```

---

## Part 1 Deliverables

- **ERD:** [`/docs/RaceDay_ERD.png`](./docs/RaceDay_ERD.png) — six entities (Roles, Users, Events, Categories, Enrolments, Results) with primary keys, foreign keys, and cardinality shown for every relationship.
- **API Endpoint Plan:** [`/docs/API_Endpoint_Plan.md`](./docs/API_Endpoint_Plan.md) — every planned endpoint covering Authentication, User Profile, Events, Categories, Enrolments, and Results.
- **SQL Script:** [`/docs/RaceDay_Database.sql`](./docs/RaceDay_Database.sql) — matches the ERD exactly.

## Database Setup

1. Open SQL Server Management Studio (SSMS).
2. Open `/docs/RaceDay_Database.sql`.
3. Execute the script against your SQL Server instance. This creates the `RaceDayDB` database, all six tables, and seeds sample data (2 Organisers, 2 Participants, 3 Events, categories, and enrolments).
4. Verify the tables loaded correctly by running the verification queries commented at the bottom of the script.

---

## CI/CD

This repository uses GitHub Actions (`.github/workflows/dotnet-ci.yml`) for continuous integration. It runs on every push and pull request to `main`. During Part 1, there is no C# project yet, so the workflow detects this and passes automatically. From Part 2 onward, it will restore, build, and run tests against the actual API project.

**Successful build screenshot:**

> <img width="1361" height="632" alt="Screenshot 2026-09-04 224857" src="https://github.com/user-attachments/assets/4a21c612-39d3-43ac-812a-1415b91c4530" />


---

## Video Walkthrough

An unlisted YouTube video walking through the planning documents, ERD decisions, endpoint plan choices, and a live run of the SQL script in SSMS:

> https://youtu.be/H3CplLZSQmk

---

## AI Tool Disclosure

AI tool (Claude) was used during the planning process to assist with drafting the ERD structure. All output was reviewed, adapted, and verified against the assignment requirements before submission.
Update README for Part 1
