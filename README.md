# Talaan | Attendance Management Information System

Attendance management for administrators and teachers, with email notices to guardian contacts. Built with ASP.NET Core 10, Next.js 16, PostgreSQL, and Docker Compose. See the [full documentation](DOCUMENTATIONS.md) for the module guide, database, screenshots, and Cloudflare testing setup.

## Start

1. Copy `.env.example` to `.env` and set strong `POSTGRES_PASSWORD` and `ADMIN_PASSWORD` values. Configure the SMTP variables for invitations and password reset.
2. Run `docker compose up --build`.
3. Open `http://localhost:3000` and sign in with `ADMIN_EMAIL` and `ADMIN_PASSWORD`.

Real credentials belong in the ignored `.env` file. `.env.example` contains placeholders only. If SMTP is not available during local development, set `EMAIL_HOST=mailpit`, `EMAIL_PORT=1025`, `EMAIL_USE_SSL=false`, and leave `EMAIL_USERNAME` blank; Mailpit's local inbox is at `http://localhost:8025`.

Teachers and administrators can update their display name under **Settings → Profile**. Their login email is read-only there. **Settings → Password** sends a six-digit code to the signed-in account's registered email; a valid code and strong new password complete the change. The code expires after 10 minutes and allows five verification attempts. After changing SMTP settings in `.env`, recreate the backend container with `docker compose up -d --force-recreate backend`.

The API health endpoint is `http://localhost:5080/api/health`. The API applies EF Core migrations and creates the initial administrator on first startup.

## Roles and setup order

- Administrator: create teachers in User Management; create, edit, archive, and restore grades, sections, and schedules in Academics; add students and guardian details from Attendance → Class Records.
- Teacher: open assigned sections, add or import students, and record attendance for the current school day. API authorization checks schedule ownership.
- Guardian contact: receives Present, Absent, Late, and Excused attendance email for linked children; the message names the subject and includes the reason for Excused. Guardian contacts do not sign in.

Attendance opens by grade and section. Class Records contains one student table with status choices and student actions. Each submission creates a numbered, immutable snapshot visible in **Report → grade → section → submission history**. Administrators can correct a past day with a required reason; teachers can submit only for the current school day. Academic records already used by students or attendance are archived rather than deleted. A schedule with attendance history cannot change teacher, subject, section, or time; create a new schedule instead.

Passwords require 12 characters with upper and lower case letters, a number, and a symbol. Reset codes expire after 10 minutes and have a five-attempt limit. Activation links expire after 48 hours.

CSV import columns are `firstName,lastName,sectionId,guardianName,guardianEmail`. Download the template from the section roster in Attendance; its section ID is prefilled. Student IDs are generated automatically in the form `STD-0000001`. Preview the CSV before importing; invalid rows are skipped. Teachers may import only into assigned sections.

## Local development

Run PostgreSQL with Compose, then start the API with `ConnectionStrings__Default` configured and `dotnet run --project backend/AMIS.API`. Start the frontend with `cd frontend && npm install && npm run dev`. The browser uses `/api` on the web origin; `API_PROXY_TARGET` defaults to `http://localhost:5080` for local development.
