# Request

Create a repo-local stdio MCP server that helps Codex understand and safely use this fork's documented Tabular Editor CLI usage.

## Requirements Summary

- Use Python, FastMCP, and uv unless repo conventions contradict that.
- Keep the server small and repo-local.
- Discover CLI documentation from repo files instead of hardcoding usage blindly.
- Expose tools for CLI help, command listing, examples, task explanation, validation, and safe dry-run-first execution.
- Reject shell syntax and arbitrary command execution.
- Add tests, README/setup instructions, and Codex MCP config guidance.

