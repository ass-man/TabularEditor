---
name: review-change
description: Use when reviewing a Tabular Editor feature branch or diff.
---

# Review Change

Use when reviewing a feature branch.

## Must Do

- Read `AGENTS.md`, feature `request.md`, feature `plan.md`, and feature `implementation-log.md` if present.
- Compare diff to request and plan.
- Prioritize concrete bugs, regressions, missing tests, scope creep, and validation gaps.
- Keep findings actionable with file and line references.
- Update `review.md` when performing a documented feature review.

## Must Not Do

- Do not focus on style unless it affects correctness or maintainability.
- Do not ask for broad refactors unless required for correctness.
- Do not approve unvalidated high-risk behavior silently.
