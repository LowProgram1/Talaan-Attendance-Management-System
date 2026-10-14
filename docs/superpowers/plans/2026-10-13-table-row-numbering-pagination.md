# Table Row Numbers and Pagination Implementation Plan

> **For agentic workers:** Execute this plan natively in the current session, following each task and reviewing the changes before completion.

**Goal:** Give every tabular module a numbered first column and consistent pagination across pages.

**Architecture:** Reuse the existing `Pagination` component and its 20-row `PAGE_SIZE`. Each table displays a `#` column with a number based on its filtered result order and current page. Keep the current local or API-backed pagination behavior of each screen.

**Tech Stack:** Next.js, React, TypeScript, existing CSS and pagination component.

**Spec:** Approved in-chat design from 2026-10-13. Scope: Attendance grade and section tables, Class Records student table, Academics grade/section/schedule tables, Reports, and User Management. Alerts are a card list, not a table, so it is outside this change.

## Global Constraints

- Keep the existing page size at 20 rows.
- Use one-based row numbers and continue numbering across pages.
- Keep all existing search, filtering, attendance, editing, and archive behavior intact.
- Do not change API contracts or add dependencies.
- Do not add or run tests unless the user asks.

## Review Focus

- When a user changes pages, row numbers continue from the previous page rather than restarting at 1.
- When filtering or changing a report period, numbering starts again at 1 for the filtered result set.
- API-paginated student records and locally sliced attendance rosters both number rows correctly.
- After data changes shrink a list, the page stays within the current last page.

## Task 1: Number and paginate Academics, Reports, and User Management tables

**Files:**
- Modify: `frontend/src/app/app/[view]/academics-view.tsx`
- Modify: `frontend/src/app/app/[view]/reports-view.tsx`
- Modify: `frontend/src/app/app/[view]/users-view.tsx`
- Reuse: `frontend/src/app/app/[view]/pagination.tsx`

- [x] Import `Pagination` and `PAGE_SIZE` in Academics; use them for slicing, compute a clamped current page from the active/archive filtered rows, and replace the custom pagination markup.
- [x] Add a `#` header and the page-offset row number as the first cell in each Academics tab, Reports, and User Management.
- [x] Keep pagination total counts based on the full filtered row set (not the current page).
- [x] Review the rendered JSX and diff for column alignment and preserved existing actions.

## Task 2: Number Attendance grade and section tables

**Files:**
- Modify: `frontend/src/app/app/[view]/page.tsx`

- [x] Add a `#` header and page-offset number to the grade table and section/schedule table.
- [x] Keep the existing `PAGE_SIZE` slicing and shared pagination controls; base numbering on `currentGradePage` or `currentSectionPage` respectively.
- [x] Review clickable rows and keyboard activation to ensure the new leading cell does not change row behavior.

## Task 3: Number Class Records and standardize its pagination

**Files:**
- Modify: `frontend/src/app/app/[view]/class-records.tsx`
- Reuse: `frontend/src/app/app/[view]/pagination.tsx`

- [x] Import `Pagination` and `PAGE_SIZE`; keep API page requests aligned with its existing 20-row response and use the shared size for locally sliced scheduled rosters and total-page calculations.
- [x] Add a `#` header and row number to the student table, using the current page offset for API-paginated results, archived records, and locally sliced attendance rosters.
- [x] Replace conditional custom Previous/Next markup with the shared pagination component.
- [x] Review search, archived records, and attendance selection rendering for consistent page boundaries and numbering.

## Final Review

- [x] Search `frontend/src` for every `<table>` and confirm each has a leading `#` column.
- [x] Confirm each data table that can exceed one page uses the shared 20-row pagination controls.
- [x] Run `git diff --check`; do not run a test suite unless requested.
