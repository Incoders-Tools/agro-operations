# Public platform contract — sanitized T1

This repository contains code and synthetic fixtures only. Private discovery notes, client names, paths, PDFs, real rows, prescriptions and credentials never belong here. Product name and domain rules await owner validation.

## Specs
- S1: ASP.NET Core Web API (.NET 10) is the selected independent backend; PostgreSQL is the intended persistence engine.
- S2: Establish a versioned `/api/v1` contract for future web and mobile consumers. A non-sensitive health endpoint can be public; domain endpoints must default to protected, not anonymously writable.
- S3: Provide local development configuration without secrets in Git, deterministic smoke tests, an initial container-ready structure and a small README. This unit does not implement real client data import, auth, domain CRUD or Railway deployment.
- S4: Keep all public examples entirely invented; no real client-specific identifiers or treatment values.

## Tasks
- T1: Create minimal API solution and focused automated checks for health and versioned contract. No fake implementation of identity or business persistence.

## Log
- Source is a private specification in a different repository; sensitive original requests are intentionally not replicated here. This file is the public handoff boundary, not authorization for deployment or real-data exposure.
