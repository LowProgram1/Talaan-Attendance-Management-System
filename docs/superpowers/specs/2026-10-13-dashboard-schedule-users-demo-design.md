# Dashboard, Schedule, User Directory, and Demo Class Update

## Goal

Improve the administrator dashboard with today's absent count, support a usable weekly timetable, make user records easier for administrators to find, and prepare one realistic class for teacher `jerald.kasim@dict.gov.ph` with 20 students and a guardian notification contact.

## Current State

- The dashboard API reports today's present count, while the administrator metric cards do not include today's absent count.
- `Schedule` stores a section, teacher, subject, and start/end time, but no weekday. Dashboard and attendance pages consume the same schedule list.
- The Academics schedule editor accepts a free-text subject and start/end times.
- User Management lists all users and supports edit/delete, but has no search or role filter. Its API and route require administrator access.
- `jerald.kasim@dict.gov.ph` is a Teacher account with no assigned section or schedule. Grade 1 / Rizal (room 101) currently has 14 students.

## Design

### Dashboard attendance metric

Add `absentToday` to the dashboard response using the same school date and authorization scope as `attendanceToday`. Place an “Absent Today” card immediately after “Present Today” in the administrator card row. Leave the teacher card row unchanged.

### Weekly schedules

Add a nullable weekday field to schedules and a database migration. Null preserves the current meaning of existing schedules (available every day). New schedules created through the UI choose a weekday. Schedule overlap checks consider the weekday; a legacy all-day schedule conflicts with any new schedule for that teacher or section at an overlapping time.

Show the weekday in Academic schedule lists and forms. Filter “Today’s Classes” and the attendance section schedule choices to today's weekday, while retaining legacy daily schedules. Keep custom subject entry available, and seed these five standard subjects for the requested class:

| Day | Subject | Time |
| --- | --- | --- |
| Monday | Science | 8:00–10:00 AM |
| Tuesday | Math | 8:00–10:00 AM |
| Wednesday | English | 8:00–10:00 AM |
| Thursday | Filipino | 8:00–10:00 AM |
| Friday | Values | 8:00–10:00 AM |

Each subject meets for two hours once per week, rather than placing all subjects on the same day.

### User Management search and filter

Add a client-side search across full name and email plus a role filter for Administrator, Teacher, and Guardian accounts. Keep the route and API administrator-only. This interprets “for admin and teacher” as the account roles to filter; teacher accounts do not receive account-management privileges.

### Teacher's Grade 1 class and guardian contact

Assign `jerald.kasim@dict.gov.ph` as adviser to the existing Grade 1 / Rizal section (room 101), increase its roster from 14 to 20 with six randomly named demo students, and attach guardian `Jerald Kasim` (`jerald11kasim@gmail.com`) to one student. The existing `jerald.kasim@gmail.com` alert-test contact remains unchanged. This selected class intentionally has 20 students, overriding the earlier 10–15-per-section demo range for this one section.

## Compatibility and data safety

- A migration adds weekday as nullable; it does not rewrite existing schedule weekdays.
- Existing daily schedules remain visible on every weekday.
- The timetable records are scoped to the existing Grade 1 / Rizal section and teacher account.
- User Management remains administrator-only.
- The existing Jerald guardian alert-test account is not replaced by the newly requested `jerald11kasim@gmail.com` contact.

## Acceptance Criteria

1. Administrators see Present Today and Absent Today cards next to one another with counts from today's records.
2. Teacher dashboards retain their current metric cards.
3. New schedules store a weekday; the five seeded Grade 1 / Rizal subjects each last two hours and are spread Monday through Friday.
4. Legacy schedules with no weekday remain available every day; new same-teacher/section schedules cannot overlap them.
5. Dashboard “Today’s Classes”, attendance schedule choices, and Academic schedule management display the correct weekdays.
6. Administrators can search users by name/email and filter by role; teachers remain unable to manage accounts.
7. Jerald's teacher account advises Grade 1 / Rizal with 20 students, and one student is linked to `jerald11kasim@gmail.com` as guardian.
