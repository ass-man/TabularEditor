# Implementation Log

## Discoveries

- `USAGE.TXT` exists but is empty.
- The most complete CLI usage source is `TabularEditor/CommandLineHandler.cs`, specifically `OutputUsage()`.
- Agent docs add important safety/workflow guidance for deploy (`-D` with `-F`, `-E`, `-W`), schema check (`-SC`), and reusable scripts (`-S`).
- `TabularEditorTest/CLITests.cs` contains practical examples for script execution, deployment, build, and folder save behavior.
- 2026-05-09: The CLI does not expose native object-edit subcommands. MCP object tools therefore generate constrained C# script templates and run them through `-S`; write operations save through `-D` only when `dry_run=false`.

## Changed Files

- `.codex/config.toml`: added `mcp_servers.tabulareditor-cli` using `uv --directory ... tools/mcp run server.py`.
- `tools/mcp/pyproject.toml`: added the Python/FastMCP package metadata and pytest optional dependency.
- `tools/mcp/server.py`: added document discovery, CLI usage indexing, MCP tool functions, command validation, dry-run-first execution, read-only model inspection tools, and constrained object-edit tools.
- `tools/mcp/tests/test_server.py`: added pytest coverage for discovery, validation, path safety, unsafe shell syntax rejection, dry-run behavior, destructive non-dry-run rejection, generated object-operation scripts, target validation, and runtime temp-script cleanup.
- `tools/mcp/README.md`: added setup, test, run, Codex registration, config, indexed source notes, target syntax, and full tool list.
- `docs/agents/features/tabulareditor-cli-mcp/request.md`: captured the feature request.
- `docs/agents/features/tabulareditor-cli-mcp/plan.md`: captured the implementation plan.

## Validation

- `uv run pytest`: not run; `uv` is not installed on PATH in this environment.
- `python --version`: Python 3.13.13.
- `python -m pytest`: initially failed 1 test, exposing a drive-qualified path safety bug.
- `python -m pytest`: passed, 7 tests.
- `python -m py_compile server.py`: passed.
- `python -m pytest -q -p no:cacheprovider` with `PYTHONDONTWRITEBYTECODE=1`: passed, 7 tests.
- 2026-05-09: `python -m py_compile server.py` with `PYTHONDONTWRITEBYTECODE=1`: passed after adding object tools.
- 2026-05-09: `python -m pytest -q -p no:cacheprovider` with `PYTHONDONTWRITEBYTECODE=1`: passed, 13 tests.
- 2026-05-09: `uv run pytest`: not run; `uv` is still not installed on PATH in this environment.

## Next Steps

- Install `uv` before using the checked-in Codex MCP config as written.
- Run `uv sync --extra test` and `uv run pytest` once `uv` is available.
