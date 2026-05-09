# Tabular Editor CLI MCP Server

This repo-local MCP server exposes documented Tabular Editor 2.x CLI usage to Codex. It indexes local repository sources such as `docs/agents/**/*.md`, `AGENTS.md`, `USAGE.TXT`, `TabularEditor/CommandLineHandler.cs`, and `TabularEditorTest/CLITests.cs`.

The server uses stdio transport. It must not write logs or diagnostics to stdout because stdout is reserved for MCP JSON-RPC. Logging goes to stderr.

## Tools

CLI documentation and execution:

- `get_cli_help(command: str | None = None) -> str`
- `list_cli_commands() -> list[str]`
- `get_cli_examples(task: str | None = None) -> str`
- `explain_cli_usage(task: str) -> str`
- `validate_cli_command(command: str) -> dict`
- `run_cli_command(command: str, dry_run: bool = true) -> dict`

Read-only model inspection:

- `inspect_model(target: str) -> dict`
- `list_tables(target: str) -> dict`
- `list_columns(target: str, table: str | None = None) -> dict`
- `list_measures(target: str, table: str | None = None) -> dict`
- `list_relationships(target: str) -> dict`
- `list_partitions(target: str, table: str | None = None) -> dict`
- `list_data_sources(target: str) -> dict`
- `list_expressions(target: str) -> dict`
- `find_objects(target: str, query: str) -> dict`

Dry-run-first object operations:

- `hide_columns(target: str, column_names: list[str], table_pattern: str | None = None, dry_run: bool = true) -> dict`
- `set_table_hidden(target: str, table_names: list[str], hidden: bool = true, dry_run: bool = true) -> dict`
- `rename_table(target: str, old_name: str, new_name: str, dry_run: bool = true) -> dict`
- `rename_column(target: str, table: str, old_name: str, new_name: str, dry_run: bool = true) -> dict`
- `delete_tables(target: str, table_names: list[str], dry_run: bool = true) -> dict`
- `delete_columns(target: str, table: str, column_names: list[str], dry_run: bool = true) -> dict`
- `create_relationship(target: str, from_table: str, from_column: str, to_table: str, to_column: str, active: bool = true, cross_filtering_behavior: str = "OneDirection", dry_run: bool = true) -> dict`
- `delete_relationship(target: str, from_table: str, from_column: str, to_table: str, to_column: str, dry_run: bool = true) -> dict`
- `create_measure(target: str, table: str, name: str, expression: str, format_string: str | None = None, dry_run: bool = true) -> dict`
- `update_measure(target: str, table: str, name: str, expression: str | None = None, format_string: str | None = None, hidden: bool | None = None, dry_run: bool = true) -> dict`
- `delete_measures(target: str, table: str, measure_names: list[str], dry_run: bool = true) -> dict`
- `copy_measures(target: str, source_table: str, target_table: str, measure_names: list[str] | None = None, dry_run: bool = true) -> dict`
- `replace_tables_with_existing_tables(target: str, mapping: dict[str, str], remove_helper_date_tables: bool = true, copy_old_measures: bool = true, recreate_missing_relationships: bool = true, hide_metadata_columns: bool = true, dry_run: bool = true) -> dict`

`run_cli_command` defaults to dry-run mode. It rejects shell metacharacters, command chaining, pipelines, redirects, non-TabularEditor entrypoints, unsafe path-like arguments outside the repo, and non-dry-run write/deploy/script commands.

The object-operation tools still use the Tabular Editor CLI and generated C# scripts internally, because this repo's CLI does not expose native object-edit subcommands. The generated scripts are constrained templates with escaped string parameters and no arbitrary shell execution. Write tools default to `dry_run=true` and only save through `-D` when called with `dry_run=false`.

`target` is the Tabular Editor model target without operation switches, for example:

- `localhost WWI_Tabular`
- `localhost:59719 10736b90-fe50-4403-9ee7-ff5e8a2148ae`
- `TabularEditorTest\TestData\AdventureWorks.bim`
- `-L MyReport`

## Install Dependencies

From this directory:

```powershell
uv sync --extra test
```

## Run Tests

```powershell
uv run pytest
```

## Run The Server Manually

```powershell
uv run server.py
```

The server starts an MCP stdio session and waits for JSON-RPC messages on stdin.

## Register With Codex

From the repository root, use:

```powershell
codex mcp add tabulareditor-cli -- uv --directory C:\Users\gamer\source\repos\TabularEditor\tools\mcp run server.py
```

## Project-Local Codex Config

This repository includes a `.codex/config.toml` block equivalent to:

```toml
[mcp_servers.tabulareditor-cli]
command = "uv"
args = ["--directory", "C:\\Users\\gamer\\source\\repos\\TabularEditor\\tools\\mcp", "run", "server.py"]
enabled = true
startup_timeout_sec = 10
tool_timeout_sec = 60
```

Adjust the absolute path if the repository is cloned elsewhere.

## Indexed CLI Sources

Primary sources discovered during implementation:

- `TabularEditor/CommandLineHandler.cs`: primary `OutputUsage()` text and switch handling.
- `TabularEditorTest/CLITests.cs`: examples for scripts, deployment, build, and folder save.
- `docs/agents/04-change-patterns.md`: repo guidance for deploy/schema-check automation.
- `docs/agents/06-tabular-editor-csharp-scripting.md`: repo guidance for reusable CLI scripts.
- `docs/agents/00-start-here.md`, `01-repo-map.md`, `02-architecture-notes.md`, `03-build-and-test.md`: CLI entry points and validation context.

`USAGE.TXT` currently exists but is empty, so the server treats it as a source candidate but does not rely on it.
