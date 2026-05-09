# Tabular Editor CLI MCP Server

This repo-local MCP server exposes documented Tabular Editor 2.x CLI usage to Codex. It indexes local repository sources such as `docs/agents/**/*.md`, `AGENTS.md`, `USAGE.TXT`, `TabularEditor/CommandLineHandler.cs`, and `TabularEditorTest/CLITests.cs`.

The server uses stdio transport. It must not write logs or diagnostics to stdout because stdout is reserved for MCP JSON-RPC. Logging goes to stderr.

## Tools

- `get_cli_help(command: str | None = None) -> str`
- `list_cli_commands() -> list[str]`
- `get_cli_examples(task: str | None = None) -> str`
- `explain_cli_usage(task: str) -> str`
- `validate_cli_command(command: str) -> dict`
- `run_cli_command(command: str, dry_run: bool = true) -> dict`

`run_cli_command` defaults to dry-run mode. It rejects shell metacharacters, command chaining, pipelines, redirects, non-TabularEditor entrypoints, unsafe path-like arguments outside the repo, and non-dry-run write/deploy/script commands.

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

