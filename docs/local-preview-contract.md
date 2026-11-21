# Local review preview — sanitized T4/T9

This is a **developer-only visual prototype** using wholly fabricated clients, fields, plots and work-order examples. It is not the authenticated application, not a record of work executed, and never connects to customer data. No login bypass is enabled in the production API.

## Specs
- S1: Provide a responsive Spanish-language local page where owners can browse/search invented clients, fields and plots and inspect a fictitious plot history and WO preview. Mark the data as demo on every page; no editable real business record or unvalidated agronomic guidance.
- S2: Local access is easy without an account, but the preview server binds only 127.0.0.1 and refuses to start outside Development/local mode. It must not be used as an open Railway staging/production deployment. Authenticated data API remains untouched.
- S3: A Windows Start Menu shortcut starts the local preview via a repository-owned script and opens a browser URL. It is manually invoked, never added to Windows Startup; script resolves its own repository path, checks prerequisites and clearly reports port conflicts/failures. Start/stop should be documented.
- S4: Public code and demo values contain no real client details; documents, Dropbox paths and credentials never enter the preview or build output. Add tests for public UI rendering/search behavior and a functional browser check if available.

## Tasks
- T4: Build the smallest reviewable web preview using Next.js and synthetic fixtures, with responsive design, accessible navigation and focused checks.
- T9: Implement a bounded local launcher and create the explicitly authorized local Windows Start Menu shortcut on C:, then verify it opens the preview only on loopback.

## Log
- The original request and client source paths remain in the separate private discovery repository. This public handoff is intentionally sanitized.
