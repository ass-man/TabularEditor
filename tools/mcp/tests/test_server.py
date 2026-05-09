from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

import server


def test_cli_doc_discovery_finds_usage_and_sources():
    server.reset_index()
    index = server.get_index()

    assert "-D / -DEPLOY" in index.usage_text
    assert any("-S / -SCRIPT" in command for command in index.commands)
    assert "TabularEditor/CommandLineHandler.cs" in index.sources
    assert "docs/agents/04-change-patterns.md" in index.sources


def test_validate_documented_schema_check_command():
    command = r"TabularEditor.exe TabularEditorTest\TestData\AdventureWorks.bim -SC"

    result = server.validate_cli_command(command)

    assert result["valid"] is True
    assert result["confidence"] in {"medium", "high"}
    assert "TabularEditor/CommandLineHandler.cs" in result["sources"]


def test_rejects_unsafe_shell_syntax():
    command = r"TabularEditor.exe TabularEditorTest\TestData\AdventureWorks.bim -SC && whoami"

    result = server.validate_cli_command(command)

    assert result["valid"] is False
    assert any("Rejected shell syntax" in warning for warning in result["warnings"])


def test_rejects_path_outside_repo():
    command = r"TabularEditor.exe C:\Windows\win.ini -SC"

    result = server.validate_cli_command(command)

    assert result["valid"] is False
    assert any("outside the repository" in warning for warning in result["warnings"])


def test_dry_run_does_not_execute(monkeypatch: pytest.MonkeyPatch):
    def fail_run(*args, **kwargs):
        raise AssertionError("subprocess.run should not be called during dry_run")

    monkeypatch.setattr(subprocess, "run", fail_run)
    command = r"TabularEditor.exe TabularEditorTest\TestData\AdventureWorks.bim -SC"

    result = server.run_cli_command(command, dry_run=True)

    assert result["dry_run"] is True
    assert result["valid"] is True
    assert result["exit_code"] is None
    assert "Dry run only" in result["stdout"]


def test_non_dry_run_rejects_destructive_command():
    command = r"TabularEditor.exe TabularEditorTest\TestData\AdventureWorks.bim -D localhost AdventureWorks -F -E -W"

    result = server.run_cli_command(command, dry_run=False)

    assert result["dry_run"] is False
    assert result["exit_code"] is None
    assert "Refusing to execute non-dry-run command" in result["stderr"]


def test_repo_local_path_safety_allows_repo_file():
    path = Path("TabularEditorTest") / "TestData" / "AdventureWorks.bim"
    command = "TabularEditor.exe " + str(path) + " -SC"

    result = server.validate_cli_command(command)

    assert result["valid"] is True

