from __future__ import annotations

import subprocess
from pathlib import Path
from types import SimpleNamespace

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


def test_target_args_reject_operation_switches():
    with pytest.raises(ValueError, match="operation switches"):
        server._parse_target_args("localhost WWI_Tabular -D")


def test_hide_columns_dry_run_generates_safe_script():
    result = server.hide_columns(
        "localhost WWI_Tabular",
        ["Valid From", "Lineage Key"],
        table_pattern="D_",
        dry_run=True,
    )

    assert result["dry_run"] is True
    assert result["save"] is True
    assert "-D" in result["command"]
    assert 'new[] { "Valid From", "Lineage Key" }' in result["script"]
    assert 'string tablePattern = "D_";' in result["script"]
    assert "Dry run only" in result["stdout"]


def test_create_measure_dry_run_escapes_expression():
    result = server.create_measure(
        "localhost WWI_Tabular",
        "F_Sale",
        "Quoted",
        'SUM("F_Sale"[Amount])',
        format_string="$#,0",
        dry_run=True,
    )

    assert result["dry_run"] is True
    assert 'AddMeasure("Quoted", "SUM(\\"F_Sale\\"[Amount])")' in result["script"]
    assert 'measure.FormatString = "$#,0";' in result["script"]


def test_replace_tables_dry_run_contains_mapping_and_summary():
    result = server.replace_tables_with_existing_tables(
        "localhost:59719 model-id",
        {"D_City": "common D_City", "F_Sale": "finance F_Sale"},
        dry_run=True,
    )

    assert result["dry_run"] is True
    assert 'pairs.Add("D_City", "common D_City");' in result["script"]
    assert 'pairs.Add("F_Sale", "finance F_Sale");' in result["script"]
    assert "CLONED_MEASURE" in result["script"]
    assert "RECREATED_RELATIONSHIP" in result["script"]
    assert "SUMMARY|ClonedMeasures=" in result["script"]


def test_list_tables_uses_read_only_model_script(monkeypatch: pytest.MonkeyPatch):
    calls = []

    def fake_run_model_script(target, script, save, dry_run):
        calls.append((target, script, save, dry_run))
        return {"ok": True}

    monkeypatch.setattr(server, "_run_model_script", fake_run_model_script)

    result = server.list_tables("localhost WWI_Tabular")

    assert result == {"ok": True}
    assert calls[0][0] == "localhost WWI_Tabular"
    assert calls[0][2] is False
    assert calls[0][3] is False
    assert "foreach(var table in Model.Tables" in calls[0][1]


def test_run_model_script_non_dry_run_uses_temp_script_and_cleans_up(monkeypatch: pytest.MonkeyPatch, tmp_path: Path):
    captured = {}

    def fake_run(argv, **kwargs):
        script_path = Path(argv[argv.index("-S") + 1])
        captured["script_path"] = script_path
        captured["script_text"] = script_path.read_text(encoding="utf-8")
        captured["argv"] = argv
        return SimpleNamespace(stdout="ok", stderr="", returncode=0)

    monkeypatch.setattr(server, "RUNTIME_DIR", tmp_path)
    monkeypatch.setattr(server, "_resolve_entrypoint", lambda entrypoint: tmp_path / "TabularEditor.exe")
    (tmp_path / "TabularEditor.exe").write_text("fake", encoding="utf-8")
    monkeypatch.setattr(server.subprocess, "run", fake_run)

    result = server._run_model_script("localhost WWI_Tabular", 'Output("x");', save=True, dry_run=False)

    assert result["exit_code"] == 0
    assert captured["script_text"] == 'Output("x");'
    assert captured["argv"][-1] == "-D"
    assert not captured["script_path"].exists()
