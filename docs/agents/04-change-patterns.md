# Change Patterns

Starter file for durable implementation patterns. Add only repeated, verified patterns. Keep entries short and practical.

## Add A Pattern When

- The same approach was used successfully in multiple changes, or
- A future agent would otherwise need to rediscover non-obvious repo behavior, and
- The pattern is verified by code inspection, tests, or successful validation.

## Pattern Template

### Pattern Name

- Applies to: files/areas.
- Use when: specific trigger.
- Steps: concise sequence.
- Tests: targeted validation.
- Pitfalls: known hazards.

## Current Patterns

### Tabular Model Deployment Through CLI

- Applies to: local SSAS/tabular model setup, model metadata deployment, schema-check validation, reusable model editing scripts.
- Use when: creating, updating, or validating a tabular model for repo or agent test workflows.
- Steps:
  - Prefer Tabular Editor CLI for build/deploy/schema-check workflows instead of sending ad hoc TMSL/XMLA directly through ADOMD.
  - Generate or update a model artifact first (`Model.bim`, folder model, or TMDL), then deploy it with `TabularEditor.exe` using `-D`.
  - Use full deployment flags when replacing a local test model: `-F` for full deploy, plus `-E` and `-W` so SSAS errors and unprocessed/problem objects are surfaced in automation.
  - Use `-S` for reusable C# model editing scripts that should run before build/deploy, and save those scripts in a durable repo location near the workflow they support.
  - Keep saved scripts model-agnostic. Do not hardcode table, column, measure, relationship, database, or server names in reusable scripts.
  - Do not save one-time repair/editing scripts whose only purpose is a local cleanup, migration, or emergency fix. Keep those commands in the conversation/implementation log only if they explain what was changed.
- Tests:
  - Run `TabularEditor.exe <model> -SC` after source schema changes when the model uses provider data sources.
  - After deployment, process/refresh the target SSAS database and run a small DAX smoke query for representative measures.
- Pitfalls:
  - `TabularEditor.exe` is a WinForms executable; use `Start-Process -Wait -NoNewWindow -PassThru` in PowerShell automation and check `ExitCode`.
  - The CLI deploys or modifies an existing model artifact. It does not by itself reverse-engineer a SQL database into a complete model without a model artifact or script.
