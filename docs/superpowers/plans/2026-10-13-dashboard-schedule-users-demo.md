# Dashboard, Schedule, User Directory, and Demo Class Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Finish the approved dashboard metric, weekday-aware schedules, user directory search and role filtering, and seed Jerald Kasim's Grade 1 / Rizal class with 20 students and its weekly subjects.

**Architecture:** Extend the existing ASP.NET Core / EF Core schedule model with an optional weekday, preserving null as the existing daily schedule behavior. Keep account filtering client-side within the existing admin-only User Management screen, and extend the idempotent demo data factory for the requested class records. The previously implemented Absent Today dashboard card is retained and verified as part of the dashboard work.

**Tech Stack:** ASP.NET Core, EF Core, PostgreSQL, Next.js, React, TypeScript, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-13-dashboard-schedule-users-demo-design.md`

## Global Constraints

- Existing schedules with no weekday remain available every day.
- New schedules created through the UI must select a weekday.
- A legacy daily schedule conflicts with overlapping time for the same teacher or section on any weekday.
- User Management remains administrator-only; search covers full name and email, and role options are Administrator, Teacher, and Guardian.
- The requested class is Grade 1 / Rizal, room 101, advised by `jerald.kasim@dict.gov.ph`, with 20 students.
- Its five weekly subjects are Monday Science, Tuesday Math, Wednesday English, Thursday Filipino, and Friday Values, each 8:00–10:00 AM.
- Keep `jerald.kasim@gmail.com` unchanged; add the distinct guardian contact `jerald11kasim@gmail.com` for Jerald Kasim.
- Do not add or run tests; verify with builds, application health, and focused read-only data queries.

## Review Focus

- **Existing null-weekday schedule:** it must still display and be selectable every day; verify with API data and the schedule consumers.
- **Overlapping schedule with a legacy daily entry:** it must be rejected for the same teacher or section on all weekdays; verify the overlap logic through code review and focused API inspection without adding/running tests.
- **Weekday boundary in attendance and dashboard:** today's schedule list must use the school's current weekday and include legacy daily schedules; verify API/UI behavior with the local application.
- **Role search and access boundary:** text search and role filtering must work together, while teacher accounts still cannot access the management route/API; verify route guards and UI filtering.
- **Idempotent data seeding:** rerunning the factory must not duplicate students, guardian contacts, or timetable entries; verify counts before and after a repeated seed operation.

## File Structure

- `backend/AMIS.API/Models.cs`: optional weekday property on `Schedule`.
- `backend/AMIS.API/School.cs`: schedule input contract and dashboard schedule response usage; retain existing `absentToday` implementation.
- `backend/AMIS.API/Academics.cs`: schedule API input/output and weekday-aware overlap validation.
- `backend/AMIS.API/Attendance.cs`: schedule choices constrained to today plus legacy daily schedules.
- `backend/AMIS.API/Migrations/`: generated EF Core migration for nullable weekday.
- `frontend/src/app/app/[view]/academics-view.tsx`: weekday schedule form and weekday display.
- `frontend/src/app/app/[view]/dashboard-view.tsx`: today's classes filtered by weekday; verify Absent Today card placement.
- `frontend/src/app/app/[view]/page.tsx`: today's weekday filtering for attendance schedule choices if this component owns that list.
- `frontend/src/app/app/[view]/users-view.tsx`: name/email search and role filter.
- `backend/AMIS.API/DemoDataFactory.cs`: idempotent data reconciliation for the named teacher, section, students, guardian, and weekly schedule.
- `README.md`: document the demo data factory behavior and local seed invocation if it changes.

## Task 1: Add Optional Weekday to the Schedule API and Database

**Files:**
- Modify: `backend/AMIS.API/Models.cs`
- Modify: `backend/AMIS.API/School.cs`
- Modify: `backend/AMIS.API/Academics.cs`
- Create: EF Core migration under `backend/AMIS.API/Migrations/`

**Interfaces:**
- `Schedule.DayOfWeek` is nullable `System.DayOfWeek?`; `null` means the existing daily schedule behavior.
- Schedule create/update API accepts nullable weekday for compatibility with existing clients; the new UI supplies a concrete weekday.
- Schedule response exposes `dayOfWeek` as a weekday name or `null`.

- [x] Inspect the project EF Core migration naming and serialization conventions; generate the migration adding a nullable weekday column without backfilling existing rows.
- [x] Add nullable `DayOfWeek` to the schedule entity and add the optional property to the schedule API request/response shape.
- [x] Update create and update handlers to persist weekday, and update overlap checks so entries conflict when their times overlap and either weekday is null or both weekdays match. Apply the existing teacher and section conflict rules.
- [x] Review migration and API diff; confirm old rows remain null and existing daily semantics remain intact.

## Task 2: Show Weekdays and Filter Today's Schedule Consumers

**Files:**
- Modify: `frontend/src/app/app/[view]/academics-view.tsx`
- Modify: `frontend/src/app/app/[view]/dashboard-view.tsx`
- Modify: `frontend/src/app/app/[view]/page.tsx`
- Modify: `backend/AMIS.API/Attendance.cs` only if filtering belongs at the API boundary.

**Interfaces:**
- The schedule form submits a weekday name matching the API's enum JSON representation.
- Dashboard and attendance schedule consumers show records where `dayOfWeek` matches the current school weekday or is `null`.

- [x] Add a weekday selector to the schedule form with Monday through Sunday options; require a selection for new UI-created schedules.
- [x] Add a weekday column/label to schedule management, displaying legacy null values as “Every day”.
- [x] Filter Dashboard “Today's Classes” to today's weekday plus legacy daily entries.
- [x] Filter attendance schedule choices to today's weekday plus legacy daily entries, using the Asia/Manila school date where date calculation occurs in the browser.
- [x] Confirm the previously implemented Absent Today card remains beside Present Today for administrators and that teacher metrics stay unchanged.

## Task 3: Add User Management Search and Role Filter

**Files:**
- Modify: `frontend/src/app/app/[view]/users-view.tsx`
- Inspect only: `backend/AMIS.API/Auth.cs` and existing route guard; modify only if the current admin restriction is not enforced.

**Interfaces:**
- Search input filters loaded user records by case-insensitive full name or email substring.
- Role filter values are All, Administrator, Teacher, and Guardian; both controls compose before rendering/pagination.

- [x] Add an accessible search field and role selector to the existing User Management toolbar.
- [x] Apply combined search and role predicates to the existing user collection, preserving current create/edit/delete behavior and pagination semantics.
- [x] Inspect the existing API and route authorization and confirm only administrators can access user management; do not grant teachers access.

## Task 4: Reconcile Jerald's Class Through the Demo Data Factory

**Files:**
- Modify: `backend/AMIS.API/DemoDataFactory.cs`
- Modify: `README.md` if factory documentation needs updating.

**Interfaces:**
- Existing demo seed entry point remains the single documented invocation and can be run repeatedly without duplicating this class data.
- The factory ensures one adviser assignment, 20 active students in Grade 1 / Rizal, the distinct guardian record, and five weekday schedule rows.

- [x] Extend the existing seed reconciliation to find `jerald.kasim@dict.gov.ph` and Grade 1 / Rizal room 101; assign Jerald as adviser, replacing the section's randomly assigned demo adviser if needed.
- [x] Ensure the roster contains exactly 20 active student records by adding six random professional/demo student names to the current 14; do not delete or alter existing students.
- [x] Ensure guardian `Jerald Kasim` with `jerald11kasim@gmail.com` exists as a separate guardian identity, and link it to one deterministic student without changing `jerald.kasim@gmail.com`.
- [x] Ensure exactly one matching schedule per weekday/subject/time pair: Mon Science, Tue Math, Wed English, Thu Filipino, Fri Values, 8:00–10:00 AM, assigned to Jerald and the Grade 1 / Rizal section.
- [x] Update factory usage documentation and run the documented local seed command after the migration has been applied.

## Task 5: Build and Verify the Integrated Local Application

**Files:**
- Review all files listed above; no additional implementation files expected.

**Interfaces:**
- Local frontend remains available at `http://localhost:3000` and backend health at `http://localhost:5080/api/health`.

- [x] Build the backend and frontend using the repository's established commands; do not run tests.
- [x] Rebuild/restart the local Docker Compose services and confirm both frontend and backend health endpoints return successfully.
- [x] Query the local database read-only to confirm teacher assignment, 20 roster records, guardian email linkage, and five weekday schedule rows; rerun the seed command once and confirm counts remain unchanged.
- [x] Inspect the final diff and run `git diff --check` to catch whitespace errors.
