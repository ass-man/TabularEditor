# Plan

## Purpose

Provide a local MCP server that exposes Tabular Editor CLI usage from this fork's own docs, command-line handler, and CLI tests so Codex can choose safer CLI commands with source attribution.

## Current Understanding

- Agent docs identify CLI entry points as `TabularEditor/Program.cs`, `TabularEditor/CommandLineHandler.cs`, `TabularEditor/ConsoleHandler.cs`, and `TabularEditorTest/CLITests.cs`.
- `USAGE.TXT` exists but is empty.
- `CommandLineHandler.OutputUsage()` contains the primary usage text.
- Repo docs describe deployment/schema-check/script practices under `docs/agents/04-change-patterns.md` and `docs/agents/06-tabular-editor-csharp-scripting.md`.
- Existing `.codex/config.toml` is minimal and can carry a repo-local MCP server block.

## Proposed Design

- Add `tools/mcp/` as the repo-local MCP package.
- Implement `server.py` with:
  - deterministic document discovery over root docs, `docs/`, `Documentation/`, `.codex/`, and targeted CLI source/test files;
  - extraction of usage text, CLI switches, examples, snippets, and sources;
  - FastMCP stdio tool registration;
  - validation using argv parsing and explicit rejection of shell metacharacters;
  - dry-run-first execution using `subprocess.run(..., shell=False)`.
- Add pytest coverage for discovery, validation, path safety, unsafe syntax rejection, and dry-run behavior.
- Add `tools/mcp/README.md` with setup, test, run, and Codex registration instructions.
- Update `.codex/config.toml` with a `tabulareditor-cli` server entry.

## Validation

- Run `uv run pytest`.
- If dependency installation is unavailable, run the tests with the available Python/pytest path and record the limitation.

## Progress

- 2026-05-09: Read agent docs, top-level README, empty `USAGE.TXT`, CLI tests, and command-line handler usage text.

