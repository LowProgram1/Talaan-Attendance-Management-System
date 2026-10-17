# Student Attendance Management Information System

A school attendance workspace for administrators and teachers, with email updates for parents and guardians. The system brings student records, class schedules, attendance entries, and reports together.

This README provides the project overview and operating guide. See [DOCUMENTATION.md](DOCUMENTATION.md) for the detailed feature descriptions, database schema and ER diagram, recorded verification results, and references. The [documentation website](https://lowprogram1.github.io/Talaan-Attendance-Management-System/) presents the detailed guide online.

## Contents

- [Background of the project](#background-of-the-project)
- [Objectives](#objectives)
- [Scope and target users](#scope-and-target-users)
- [Technology stack](#technology-stack)
- [How to run the project](#how-to-run-the-project)
- [Using the system](#using-the-system)
- [Future enhancements](#future-enhancements)

## Background of the project

Attendance management involves more than marking students present or absent. Teachers need accurate class lists, accessible schedules, consistent student records, and a practical way to communicate attendance concerns to families. Separate spreadsheets and paper records can make these tasks repetitive and make historical information difficult to retrieve.

The Student Attendance Management Information System was developed to organize these activities in one workspace. Teachers record attendance for their assigned classes, while administrators maintain academic records, staff access, and school-wide information. Linked parents and guardians receive attendance notifications by email.

The project addresses scattered records, duplicate student entries, delayed communication, time-consuming summaries, and unclear responsibility for attendance corrections. Its intended benefits require evaluation in actual school use; the project does not claim measured improvements in attendance or workload.

## Objectives

### General objective

Provide an organized attendance system that helps school personnel maintain consistent records, monitor attendance, and communicate with families.

### Specific objectives

- Centralize student information, class lists, and schedules to reduce repeated entry and searching.
- Support student identification, duplicate detection, enrollment, and controlled transfers between sections.
- Record Present, Absent, Late, and Excused attendance with appropriate permissions and required reasons.
- Notify linked guardians when attendance is first recorded or meaningfully changed.
- Provide dashboard charts, period summaries, and CSV exports for reviewing attendance patterns.
- Retain submission revisions and correction information to support accountability.

## Scope and target users

| User | Current role and access |
| --- | --- |
| Administrator | Manages staff, academic setup, student records and transfers; reviews school-wide reports and alert logs. |
| Teacher | Signs in to manage permitted class rosters and submit current-day attendance for assigned schedules. |
| Student | Has a student record and recorded attendance; no student sign-in or dashboard is implemented. |
| Parent/guardian | Receives email notices for linked children; no guardian sign-in or dashboard is implemented. |

### Implemented capabilities

- **Staff accounts:** login/logout, invitations and activation, profile updates, password recovery by email link, and signed-in password changes by email code. Remember me saves the email and checkbox preference after a successful login; password autofill uses the browser password manager.
- **Dashboard:** role-specific totals, Present Today and Absent Today cards, Today's Classes, quick actions, community and subject charts, and attendance trends by day, week, and month.
- **Academic setup:** grades, departments, sections, rooms, advisers, and subject schedules with weekday/time validation and archive/restore controls.
- **Student List:** search and filters, generated School ID No., duplicate checks, active/unassigned enrollment, transfers, and archive/restore. Internal UUIDs are hidden in student tables.
- **Batch import:** CSV template download, file upload, row preview and validation, and import of valid rows into a selected section.
- **Attendance:** complete-roster submissions, excuse reasons, current-day teacher restrictions, administrator past-day corrections, revision checks, and submission snapshots.
- **Reports:** day/week/month summaries, attendance counts and rates, and CSV export. Submission-history endpoints exist; a complete dedicated history interface is not claimed.
- **Notifications:** guardian attendance emails and administrator alert logs with 20-row server pagination, newest first.
- **Interface:** responsive layouts, fixed navigation, numbered tables, pagination, and loading/error/empty states.

### Current boundaries

- Present/Absent Today totals count subject attendance records, not necessarily unique students. A student attending several subjects can contribute several records.
- Subject names are stored on schedules; there is no separate subject catalog or school-year enrollment-history module.
- Guardian/student portals, grading integration, online enrollment integration, and a mobile application are proposed features.
- Internal APIs exist, but dedicated external-client authentication, a published versioned API contract, and completed external integrations are not implemented. The OpenAPI package is installed without a configured served document or Swagger UI.
- Email depends on the configured SMTP service. A successful SMTP send does not prove inbox arrival; there is no durable delivery queue.
- Import supports CSV, not XLSX or multiline quoted CSV fields. Duplicate matching is based on normalized student names and guardian email, not definitive identity verification.

## Technology stack

Versions below reflect the project configuration.

| Technology | Framework/library/tool | Purpose |
| --- | --- | --- |
| C# / .NET 10 | ASP.NET Core Minimal APIs | Backend HTTP endpoints and application services. |
| Authentication | ASP.NET Core Identity 10.0.0 | Staff accounts, roles, password policy, and session cookies. |
| Data access | Entity Framework Core 10.0.0; Npgsql EF provider 10.0.0 | PostgreSQL queries, relationships, and migrations. |
| Database | PostgreSQL 17 (Alpine image) | Persistent student, academic, attendance, and account records. |
| Frontend | Next.js 16.3.8; React/React DOM 19.2.8 | App Router pages and interactive user interface. |
| Language and styling | TypeScript 5; Tailwind CSS 4; custom CSS | Typed frontend code and responsive presentation. |
| Icons and charts | Lucide React; custom SVG/CSS | Interface icons and dashboard visualizations. |
| Runtime and packaging | Node.js 24; Docker; Docker Compose | Frontend build/runtime and coordinated local services. |
| Email | .NET System.Net.Mail; Mailpit | SMTP delivery and optional local email preview. |
| Development checks | ESLint 9; xUnit; EF InMemory; Coverlet | Linting and existing backend test/coverage tooling. Tool availability is not a test-pass claim. |
| Documentation | Markdown; Mermaid; Jekyll/GitHub Pages; Python | Documentation, ER diagrams, and website-source generation. |
| Optional tools | DBeaver; Cloudflare Tunnel | Local database inspection and opt-in temporary web access. |

OpenAI Codex assisted development and documentation. Official technology references and detailed provenance are listed in [References and credits](DOCUMENTATION.md#references-and-credits).

## How to run the project

### Prerequisites

- Docker Desktop with its Linux container engine running and Docker Compose available.
- A local copy of this repository.
- Available ports: `3000` (frontend), `5080` (API), `5433` (local database access), and `8025` (Mailpit inbox).

The Docker workflow builds the application inside containers; a host .NET SDK or Node.js installation is not required for it.

### 1. Configure the environment

From the repository root, create `.env` only if it does not already exist. In PowerShell:

```powershell
if (!(Test-Path -LiteralPath .env)) { Copy-Item -LiteralPath .env.example -Destination .env }
```

Edit `.env` with your own values:

| Setting | Purpose |
| --- | --- |
| `POSTGRES_PASSWORD` | Password used by PostgreSQL and the API connection. |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Initial administrator created when the configured email does not already exist. |
| `PUBLIC_FRONTEND_URL` | Leave blank or set to `http://localhost:3000` for local activation/reset links. |
| `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_USE_SSL` | SMTP host, port, and encryption configuration. |
| `EMAIL_USERNAME`, `EMAIL_PASSWORD`, `EMAIL_FROM` | SMTP authentication and sender address. |

The administrator password must contain at least 12 characters, uppercase and lowercase letters, a number, and a symbol. Use a strong value without common password fragments. Real credentials belong only in the ignored `.env`; do not commit them. Changing initial-admin settings does not reset an existing account's password.

For local email preview, configure:

```dotenv
EMAIL_HOST=mailpit
EMAIL_PORT=1025
EMAIL_USE_SSL=false
EMAIL_USERNAME=
EMAIL_PASSWORD=
EMAIL_FROM=school@example.test
```

Mailpit captures mail locally; it does not deliver those messages to real family inboxes. Configure a real SMTP service when actual delivery is needed.

### 2. Build and start

```powershell
docker compose up -d --build
```

Startup applies EF Core migrations and initializes roles and the configured administrator. Named volumes preserve database records and data-protection keys.

| Service | Local address |
| --- | --- |
| Application | http://localhost:3000 |
| API health | http://localhost:5080/api/health |
| Mailpit inbox | http://localhost:8025 |
| PostgreSQL / DBeaver | Host `localhost`, port `5433`, database `amis`, username `amis`; password from your `.env`. |

Sign in using the configured administrator account. The application uses `/api` on the frontend origin and proxies requests to the backend.

### 3. Manage the running services

```powershell
docker compose ps
docker compose logs --tail 100 backend frontend
docker compose stop
```

After changing email settings, recreate the backend:

```powershell
docker compose up -d --force-recreate backend
```

For source changes, rerun `docker compose up -d --build`. Do not remove the database volume unless you intend to erase its stored data.

### Optional demo data

For a local/demo database only:

```powershell
docker compose run --rm backend --seed-demo-data
```

The factory creates example teachers, guardian contacts, Grades 1-6 sections with Filipino-themed names, students, and three-digit room numbers. Most sections receive 10-15 students; the configured Grade 1 / Rizal example is maintained at 20. Example subjects include Science, Math, English, Filipino, and Values, with two-hour weekday classes. It also assigns an available weekday class to active teachers without an active schedule. The factory is designed for repeated execution without duplicating its managed sample records; normal startup does not rerun it.

### Optional development outside containers

Install the .NET 10 SDK and Node.js 24. Start the database with `docker compose up -d db`. Configure the API process environment with `ConnectionStrings__Default` targeting `localhost:5433`, `FRONTEND_URL=http://localhost:3000`, and the required administrator/email values. The API does not automatically load the root Compose `.env` when run directly.

```powershell
dotnet run --project backend/AMIS.API --urls http://localhost:5080
```

In a separate terminal:

```powershell
cd frontend
npm ci
npm run dev
```

The frontend's local proxy defaults to `http://localhost:5080`; `API_PROXY_TARGET` can override it. Stop the corresponding Docker application services before running local processes on the same ports. SMTP host `mailpit` is a Docker service name, so a host-run API needs a reachable SMTP host rather than that container-only hostname.

## Using the system

1. Invite staff from **User Management** and complete account activation.
2. Create grades and sections, assign advisers, and configure teacher schedules in **Academics**.
3. Add students through **Student List**, or add/enroll students through the permitted Attendance class. Existing unassigned students can be selected from the directory.
4. Open the assigned class and record attendance. Excused entries require a reason; administrator corrections to past dates also require a reason.
5. Review dashboard summaries, reports, exported CSV files, and guardian notification results.

### Batch-import template

Download the current template from Student List or Attendance. Its columns are:

```csv
firstName,lastName,guardianName,guardianEmail
Ana,Santos,Maria Santos,maria@example.com
```

Select a destination such as **Grade 1 / Rizal** in Student List before uploading. Attendance uses the current class. Every imported row goes to that destination; the spreadsheet contains no section ID or section name. Remove the `sectionId` column from older templates, save as CSV UTF-8, review the preview, and select **Import**. Invalid rows are skipped and reported. School ID numbers are generated automatically.

### Account recovery

- **Forgot password:** sends a single-use reset link valid for 30 minutes, with New password/Confirm password fields and strength feedback.
- **Settings / Password:** uses a separate six-digit email code valid for 10 minutes, with five verification attempts.
- **Activation:** invitation links expire after 48 hours.

## Future enhancements

The following are proposed improvements, not completed integrations:

| Enhancement | Intended purpose |
| --- | --- |
| Grading System API integration | Exchange agreed student/attendance information with the existing system identified by the owner; confirm API contracts, permissions, and identifier mapping first. |
| Online Enrollment System API integration | Reuse approved enrollment data with defined synchronization, duplicate reconciliation, and ownership rules. API availability is owner-reported; no external contract is verified in this repository. |
| Parent/guardian dashboard | Allow authenticated guardians to monitor attendance and relevant information for multiple linked children. |
| Mobile application | Provide mobile access and notifications, with explicit synchronization rules if offline use is added. |
| External API readiness | Add a published versioned contract, dedicated integration authentication/scopes, and integration validation. |
| Notification reliability | Add a durable queue/outbox, retry management, and clearer delivery diagnostics. |
| Academic history and data quality | Add school-year enrollment history, a subject catalog, richer guardian management, and reviewed XLSX support. |
| Accessibility and usability | Improve keyboard and screen-reader support, chart alternatives, touch interaction, and language options based on user evaluation. |
| Performance and operations | Profile larger datasets, extend server pagination, and improve monitoring, backup/recovery procedures, and regression coverage. |

Refer to [Testing results and evaluation](DOCUMENTATION.md#testing-results-and-evaluation) for the distinction between recorded development checks and project-owner verification.
