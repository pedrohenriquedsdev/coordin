# Coordin

> A decision-support system that forecasts delivery dates, balances team capacity, and recommends optimized demand sequencing for software development teams.

Coordin helps development managers turn a list of demands, their effort estimates, and team availability into a **predictable delivery schedule**. Beyond calculating dates, it compares scenarios, highlights idle capacity and overload, and recommends alternative ways to organize the work, always leaving the final decision to the manager.

> **Project status:** 🚧 Early stage. This repository starts from an academic project (TCC) proposal. Several calculation rules are still being specified (see [Open Design Decisions](#open-design-decisions)).

---

## Table of Contents

- [Overview](#overview)
- [The Problem](#the-problem)
- [How It Works](#how-it-works)
- [Key Features](#key-features)
- [Inputs](#inputs)
- [Forecast Calculation](#forecast-calculation)
- [Sequencing Optimization](#sequencing-optimization)
- [Reports and Indicators](#reports-and-indicators)
- [Expected Outcomes](#expected-outcomes)
- [Open Design Decisions](#open-design-decisions)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [Academic Context](#academic-context)
- [License](#license)

---

## Overview

The central goal of Coordin is to give managers enough visibility to answer the product team one question with confidence:

> **"When can each demand be delivered, considering the real capacity of the teams?"**

The system takes the demands, the effort estimated by the teams, and each team's availability, and produces:

- A **delivery schedule** with forecasted completion dates.
- **Alternative scenarios** for demand sequencing.
- **Capacity insights** per team (idle periods, overload, available capacity).
- **Recommendations** that the manager can approve or reject.

## The Problem

Planning deliveries becomes hard when many demands compete for attention and several teams are involved. Not every demand needs every team: some are executed by a single team, while others require several teams working together.

Common pain points:

- No consolidated view of demands, owners, effort, and forecasted dates.
- Uneven distribution of work, with some teams idle while others are overloaded.
- Difficulty understanding the impact of new demands on an existing schedule.
- Difficulty comparing alternative sequences to find a more efficient organization.
- Delivery forecasts that depend on estimates made by the teams themselves, not only on demand priority.

The challenge is not just tracking tasks. It is **coordinating the capacity of multiple teams** to forecast and improve the department's deliveries.

## How It Works

The workflow starts with product and development alignment and ends with forecasting and tracking deliveries.

| Step | Stage | Responsibility / Outcome |
|------|-------|--------------------------|
| 1 | **Prioritization** | The development manager aligns the list of demands and their priorities with the product manager. |
| 2 | **Distribution** | The manager defines which teams will work on each demand. A demand can involve one or several teams, and a team can work on multiple demands within its capacity. |
| 3 | **Estimation** | The teams analyze the work and report the required effort (e.g., in hours or estimated duration). This information does not need to exist when the demand is received. |
| 4 | **Planning** | The manager records the information in the system, including allocation and start date. An end date can be provided when applicable. |
| 5 | **Calculation** | The system considers effort, people, teams, availability, sequence, and a safety margin to calculate forecasts. |
| 6 | **Decision** | The system presents the schedule and alternative scenarios. The manager approves or rejects the recommendations. |

## Key Features

- **Demand management** with priorities, involved teams, and effort estimates.
- **Delivery forecasting** based on effort, allocated people, availability, and execution order.
- **Automatic safety margin** that makes forecasts more conservative (it only extends the deadline, never shortens it).
- **Scenario comparison** between the current sequence and alternative orderings.
- **Sequencing recommendations** that are suggestions, never silent changes.
- **Team capacity reports** showing occupancy, idle time, and overload.
- **General planning report** to compare sequences and their impact on the department schedule.

## Inputs

A demand arrives with its priority. The remaining information is completed throughout planning, especially after the teams provide their estimates.

| Input | How it is used |
|-------|----------------|
| **Demand and priority** | Identifies the work and its order of importance, as defined in the product/development alignment. |
| **Involved teams** | Defines which teams will take part in executing each demand. |
| **Estimated effort** | Represents the work required, as estimated by the teams and recorded by the manager. |
| **Number of people / capacity** | Helps estimate the possible duration according to the resources actually allocated and available. |
| **Start date** | Indicates when execution is planned to begin. |
| **End date** *(optional)* | Lets the manager provide a date when needed, in addition to the calculated forecast. |
| **Safety margin** | Applied automatically to make the forecast more conservative. |

## Forecast Calculation

The system converts estimated effort into a forecasted duration and, from the start date and available capacity, calculates the expected completion date. The forecast takes into account:

- The participation of one or more teams.
- The number of people allocated.
- Availability of people and teams.
- The execution order of the demands.

**Safety margin:** the system automatically adds a margin to the calculated deadline. This margin must always *extend* the forecast, never reduce it, to avoid overly optimistic dates.

**A note on parallelism:** a demand estimated at 100 hours for one team may, under balanced division and parallelizable work, have its duration reduced when a second team joins. Coordin recalculates the forecast based on *effective* capacity and does **not** assume that adding a team always cuts the deadline exactly in half.

## Sequencing Optimization

Besides calculating dates for the sequence defined by the manager, Coordin analyzes alternative execution combinations and identifies those that could improve the overall result, for example by reducing total duration, bringing deliveries forward, or making better use of available capacity.

**Example scenario**

| Current sequence | Suggested alternative |
|------------------|-----------------------|
| `A → B → C → D` | `B → A → C → D` |
| Dates calculated with the current order. | New start and end dates calculated for the alternative order. |

Important principles:

- A change is a **recommendation, not an automatic modification**.
- The system shows the schedule impact of the alternative and **waits for the manager's approval or rejection**.
- Priority remains a relevant input. Any change of order is presented as a scenario to evaluate, never as a silent replacement of priority.

## Reports and Indicators

### General productivity and planning report

- Compare possible demand sequences and their delivery forecasts.
- Show the impact of alternatives on the department schedule.
- Help identify a combination that speeds up deliveries and makes better use of combined capacity.

### Individual team report

- View occupancy, availability, idle periods, and possible overload.
- Identify improvement opportunities and potential productivity gaps.
- Find teams with capacity to take on new demands or support other teams.
- Support workload redistribution based on identified availability.

## Expected Outcomes

- Greater predictability of delivery dates.
- Centralized view of demands and team capacity.
- Better synchronization between teams working on the same demand.
- Early identification of idle time, overload, and reallocation opportunities.
- Scenario comparison to support sequencing decisions.
- Clearer communication between development and product managers.

## Open Design Decisions

Some rules still need to be detailed during specification and development. They do not invalidate the proposal; they are decisions required for the calculations to be reliable.

- **Capacity model:** how to represent available hours per person and per team, considering vacations, absences, and other allocations.
- **Parallelism:** how to estimate duration reduction when several teams work on the same demand, and which activities can truly run in parallel.
- **Dependencies:** how to represent demands or stages that must wait for others to finish.
- **Safety margin:** whether it will be a percentage, a fixed value, or configurable, and at which stage it is applied.
- **Optimization criteria:** whether recommendations will prioritize total duration, priority deliveries, team utilization, or a combination.
- **Estimate updates:** how to record effort changes and compare forecasts against actual results.

## Tech Stack

The decisions and their rationale are recorded in [`docs/TDD.md`](docs/TDD.md).

| Layer | Technology |
|-------|------------|
| Backend | .NET 10 (LTS), ASP.NET Core Minimal APIs, EF Core 10 |
| Calculation engine | `Coordin.Engine`: a pure C# library with no database, HTTP, or clock access |
| Frontend | Angular 22 (standalone components, signals), TypeScript 6, Tailwind CSS 4 |
| Database | PostgreSQL 18 |
| Infrastructure | Docker (a single image with the API and the Angular build), GitHub Actions |

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 10 (the exact version is pinned in `global.json`)
- [Node.js](https://nodejs.org) 24 LTS and npm
- [Docker](https://www.docker.com/products/docker-desktop/) (local PostgreSQL and the integration tests)

### Run locally

```bash
# 1. Clone the repository
git clone https://github.com/pedrohenriquedsdev/coordin.git
cd coordin

# 2. Create your local .env and set POSTGRES_PASSWORD (letters and numbers only).
#    The .env file is ignored by Git. Never commit passwords or other secrets.
echo "POSTGRES_PASSWORD=YOUR_PASSWORD" > .env

# 3. Start PostgreSQL
docker compose up -d

# 4. Give the API its secrets as user secrets (stored outside the repository, never in a file Git tracks).
#    The password in the connection string must be the same one you set in .env.
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=coordin;Username=coordin;Password=YOUR_PASSWORD" --project src/Coordin.Api

# 5. Configure authentication (see "Authentication" below for how to generate the key).
#    Without Jwt:SigningKey the API refuses to start; without the Manager values nobody can sign in.
dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_KEY_WITH_AT_LEAST_32_BYTES" --project src/Coordin.Api
dotnet user-secrets set "Manager:Email" "you@example.com" --project src/Coordin.Api
dotnet user-secrets set "Manager:Password" "A_PASSWORD_WITH_AT_LEAST_12_CHARACTERS" --project src/Coordin.Api

# 6. Run the API (http://localhost:5080; applies the database migrations and creates the manager account on startup)
dotnet run --project src/Coordin.Api
```

In a second terminal, run the front end:

```bash
cd src/coordin-web
npm ci
npm start   # http://localhost:4200, proxies /api to the API on port 5080
```

Open `http://localhost:4200` and sign in with the `Manager:Email` and `Manager:Password` you set in step 5.

While the API runs in `Development`, the OpenAPI document is at `http://localhost:5080/openapi/v1.json` and the interactive documentation at `http://localhost:5080/swagger`. Check that everything is connected at `http://localhost:4200/api/health`, which should answer `{"status":"Healthy"}`.

### Authentication

There is a single account, the development manager's, and no sign-up screen. The account is created when the API starts, from configuration, so no credential ever passes through the repository.

| Setting | Environment variable (production) | What it is |
|---------|-----------------------------------|------------|
| `Jwt:SigningKey` | `Jwt__SigningKey` | Secret that signs the access tokens. At least 32 bytes; the API refuses to start without it. |
| `Manager:Email` | `Manager__Email` | E-mail of the manager account, created if no account exists yet. |
| `Manager:Password` | `Manager__Password` | Password of the account, at least 12 characters. |
| `Manager:ResetPassword` | `Manager__ResetPassword` | `true` replaces the password of the existing account (see below). |

`Jwt:Issuer` and `Jwt:Audience` are not secrets and live in `appsettings.json`.

Generate a random key with one of:

```bash
node -e "console.log(require('crypto').randomBytes(48).toString('base64'))"
```

```powershell
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes)
```

**How a session works:** signing in returns an access token that lives for 15 minutes and is kept only in the browser's memory (never in `localStorage`). The session is renewed silently by a refresh token in an `HttpOnly` cookie, valid for 7 days and replaced on every renewal. Reloading the page keeps you signed in. Five wrong passwords lock the account for 15 minutes, and sign-in and renewal are limited to 10 requests per minute per IP address.

**Forgot the password?** Set `Manager:Password` to the new value and `Manager:ResetPassword` to `true`, restart the API, then remove the `ResetPassword` flag. This also clears a lockout and ends every open session. Only the password changes, not the e-mail.

**Changing `Jwt:SigningKey`** invalidates the access tokens already issued; the browser renews them on its own from the refresh token, so nobody has to sign in again.

Notes:

- The refresh cookie is `Secure`. Chrome, Edge, and Firefox accept it on `http://localhost`; Safari does not, so use another browser for local development. In production the host serves HTTPS.
- If you run the built API directly (`dotnet bin/Debug/net10.0/Coordin.Api.dll`), do it from the `src/Coordin.Api` folder: the application looks for `appsettings.json` in the current folder and, without it, refuses to start because the issuer and audience are missing.

### Tests and checks

```bash
# .NET: build, formatting and tests (Docker must be running: the integration tests start a PostgreSQL container)
dotnet build Coordin.slnx
dotnet format Coordin.slnx --verify-no-changes
dotnet test Coordin.slnx

# Front end (inside src/coordin-web)
npm run lint
npm test
npm run build
```

### Regenerate the typed API client

With the API running in `Development`, from `src/coordin-web`:

```bash
npm run generate:api
```

The generated code in `src/coordin-web/src/app/api/` is committed, so a contract change shows up in the pull request diff.

### Run the production image locally

Besides `POSTGRES_PASSWORD`, the image needs the authentication values, which Compose reads from the same `.env` file (ignored by Git):

```bash
# .env
POSTGRES_PASSWORD=YOUR_PASSWORD
JWT_SIGNING_KEY=YOUR_RANDOM_KEY_WITH_AT_LEAST_32_BYTES
MANAGER_EMAIL=you@example.com
MANAGER_PASSWORD=A_PASSWORD_WITH_AT_LEAST_12_CHARACTERS
```

```bash
docker compose --profile app up -d --build   # http://localhost:8080
docker compose --profile app down
```

This builds the single image (API plus the Angular build) and runs it next to the database. It runs as `Staging`: identical to production except that HTTPS is not forced, since there is no TLS-terminating proxy in front of it locally.

If the container exits right after starting, read its log with `docker compose logs app`: a missing `JWT_SIGNING_KEY` is reported there. A missing `MANAGER_EMAIL` or `MANAGER_PASSWORD` does not stop the application, but nobody will be able to sign in. The plain `docker compose up -d` (database only) does not need any of these three values.

## Roadmap

- Finalize the specification of the open design decisions
- Domain model: demands, teams, people, capacity, and estimates
- Forecast engine (effort → duration → completion date) with safety margin
- Schedule view with the manager's sequence
- Scenario generation and comparison for alternative sequences
- Approval/rejection flow for recommendations
- General planning report
- Individual team report (occupancy, idle time, overload)
- Tracking of forecast vs. actual results

## Contributing

Contributions, ideas, and feedback are welcome. Since the project is still taking shape, please open an issue to discuss what you would like to change before submitting a pull request.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m "Add my feature"`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a pull request

## Academic Context

Coordin originated as a final undergraduate project (TCC) proposal: *Planning and Demand Optimization — a system to support delivery forecasting and the distribution of development team capacity.* The manager remains responsible for decisions; the system provides the calculations, visibility, and scenarios needed to make them.

## License

License to be defined. Add a `LICENSE` file to the repository root (for example, MIT) and update this section.
