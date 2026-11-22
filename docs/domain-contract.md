# Public domain contract — sanitized T2

This file contains invented terminology only. No customer files or copied client data are permitted.

## Specs
- S1: Model general operations hierarchy: `Client` has many `Field`s; `Field` has many `Plot`s. Give each entity an immutable generated ID and display name; plot has an area in hectares, not an application area.
- S2: Reject blank client/field/plot names, nonpositive plot areas and references to missing parents. Never delete a referenced parent as a side effect. Use explicit numeric precision and database constraints for persisted area.
- S3: Record a revision for concurrent updates. Stale updates must fail with a typed conflict rather than overwrite someone else's edits. Do not equate a plot's total area with treated area on later orders.
- S4: Database schema should be PostgreSQL-compatible with a reviewable migration. No external domain HTTP endpoints may be exposed until authentication and authorization exist. Tests exercise the public application/service boundary and verify errors and unchanged stored state for rejected mutations.
- S5: The crop catalog uses separately reviewed crop names and revision-guarded edits. Imported season strings such as multiple crops separated by a slash are evidence, not automatically verified catalog entries.

## Tasks
- T2a: Add scoped persistence/domain model and deterministic tests for S1-S4. If local Postgres is available, run focused integration tests with isolated data; otherwise report test runner unavailable and don't claim real database proof. Do not implement speculative agronomy rules, order lifecycle or private data ingestion here.
- T2b: Add `003_crops.sql`, a private crop catalog service/repository and isolated persistence tests for S5. No business HTTP routes or customer data in this slice.

## Log
- This sanitized public handoff is a subset of the private specification; actual customer paths and names are never copied here.
- Crop catalog additions are internal only; SQL migration `003_crops.sql` must run after `001_operations.sql`. Do not infer seasonal crop assignments from raw text without human validation.
