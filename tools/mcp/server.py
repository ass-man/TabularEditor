from __future__ import annotations

import logging
import os
import re
import shlex
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


LOG = logging.getLogger("tabulareditor-cli-mcp")
REPO_ROOT = Path(__file__).resolve().parents[2]
MAX_FILE_BYTES = 300_000
COMMAND_TIMEOUT_SEC = 60
RUNTIME_DIR = Path(__file__).resolve().parent / ".runtime"

CLI_TERMS = (
    "cli",
    "command line",
    "command-line",
    "tabulareditor.exe",
    "tabulareditor",
    "-deploy",
    "-build",
    "-bim",
    "-folder",
    "-tmdl",
    "-script",
    "-schemacheck",
    "-analyze",
    "deploy",
    "automation",
)

ROOT_DOCS = (
    "AGENTS.md",
    "README.md",
    "USAGE.TXT",
    "PLANS.md",
)

TARGETED_CODE_FILES = (
    "TabularEditor/CommandLineHandler.cs",
    "TabularEditor/Program.cs",
    "TabularEditor/ConsoleHandler.cs",
    "TabularEditorTest/CLITests.cs",
)

DOC_EXTENSIONS = {".md", ".txt", ".toml", ".yml", ".yaml", ".rst"}
SKIPPED_PARTS = {"bin", "obj", "packages", "artifacts", "TestResults", ".git"}
DESTRUCTIVE_SWITCHES = {
    "-S",
    "-SCRIPT",
    "-B",
    "-BIM",
    "-BUILD",
    "-FOLDER",
    "-TMDL",
    "-D",
    "-DEPLOY",
    "-FULL",
    "-OVERWRITE",
    "-O",
    "-C",
    "-CONNECTIONS",
    "-P",
    "-PARTITIONS",
    "-R",
    "-ROLES",
    "-M",
    "-MEMBERS",
    "-X",
    "-XMLA",
    "-T",
    "-TRX",
}


@dataclass(frozen=True)
class SourceDoc:
    path: Path
    text: str

    @property
    def relpath(self) -> str:
        return self.path.relative_to(REPO_ROOT).as_posix()


@dataclass
class CliIndex:
    repo_root: Path = REPO_ROOT
    docs: list[SourceDoc] = field(default_factory=list)
    usage_text: str = ""
    commands: list[str] = field(default_factory=list)
    examples: list[dict[str, str]] = field(default_factory=list)
    snippets: list[dict[str, str]] = field(default_factory=list)
    sources: list[str] = field(default_factory=list)
    known_switches: set[str] = field(default_factory=set)

    def refresh(self) -> "CliIndex":
        self.docs = list(self._load_docs())
        self.usage_text = self._extract_usage_text()
        self.commands = self._extract_commands()
        self.known_switches = self._extract_known_switches()
        self.examples = self._extract_examples()
        self.snippets = self._extract_snippets()
        self.sources = sorted({doc.relpath for doc in self.docs if self._is_relevant(doc)})
        return self

    def _load_docs(self) -> list[SourceDoc]:
        paths: list[Path] = []
        for rel in ROOT_DOCS + TARGETED_CODE_FILES:
            path = self.repo_root / rel
            if path.exists():
                paths.append(path)

        for root_name in ("docs", "Documentation", ".codex", "codemap"):
            root = self.repo_root / root_name
            if not root.exists():
                continue
            for path in root.rglob("*"):
                if path.is_file() and path.suffix.lower() in DOC_EXTENSIONS and not self._skip_path(path):
                    paths.append(path)

        seen: set[Path] = set()
        docs: list[SourceDoc] = []
        for path in sorted(paths):
            resolved = path.resolve()
            if resolved in seen or self._skip_path(path):
                continue
            seen.add(resolved)
            try:
                if path.stat().st_size > MAX_FILE_BYTES:
                    continue
                text = path.read_text(encoding="utf-8-sig", errors="replace")
            except OSError as exc:
                LOG.warning("Skipping unreadable doc %s: %s", path, exc)
                continue
            doc = SourceDoc(path=path, text=text)
            if self._is_relevant(doc) or doc.relpath in ROOT_DOCS or doc.relpath in TARGETED_CODE_FILES:
                docs.append(doc)
        return docs

    def _skip_path(self, path: Path) -> bool:
        return any(part in SKIPPED_PARTS for part in path.parts)

    def _is_relevant(self, doc: SourceDoc) -> bool:
        haystack = (doc.relpath + "\n" + doc.text[:50_000]).lower()
        return any(term in haystack for term in CLI_TERMS)

    def _extract_usage_text(self) -> str:
        handler = next((doc for doc in self.docs if doc.relpath == "TabularEditor/CommandLineHandler.cs"), None)
        if not handler:
            return ""
        match = re.search(r'Console\.WriteLine\(@"\s*(Usage:.*?)"\);', handler.text, flags=re.DOTALL)
        if not match:
            return ""
        return match.group(1).replace('""', '"').strip()

    def _extract_commands(self) -> list[str]:
        commands: list[str] = []
        if self.usage_text:
            for line in self.usage_text.splitlines():
                stripped = line.strip()
                if not stripped:
                    continue
                if stripped.startswith("TABULAREDITOR"):
                    commands.append(stripped)
                    continue
                if re.match(r"^-[A-Z0-9?]+(?:\s*/\s*-[A-Z0-9?]+)*\s+", stripped):
                    commands.append(re.sub(r"\s+", " ", stripped))

        for doc in self.docs:
            if doc.relpath.endswith("CLITests.cs"):
                for match in re.finditer(r"CommandLine\((.*?)\);", doc.text, flags=re.DOTALL):
                    command = "CommandLine(" + re.sub(r"\s+", " ", match.group(1)).strip() + ")"
                    commands.append(command)

        return _dedupe(commands)

    def _extract_known_switches(self) -> set[str]:
        switches = set(re.findall(r"(?<!\w)-[A-Za-z][A-Za-z0-9?]*", self.usage_text))
        switches.update({"-?", "-H", "/?", "/H", "HELP"})
        for doc in self.docs:
            if doc.relpath.endswith("CommandLineHandler.cs"):
                switches.update(re.findall(r'"(-[A-Za-z][A-Za-z0-9?]*)"', doc.text))
        return {switch.upper() for switch in switches}

    def _extract_examples(self) -> list[dict[str, str]]:
        examples: list[dict[str, str]] = []
        for doc in self.docs:
            lines = doc.text.splitlines()
            in_fence = False
            fence: list[str] = []
            for line in lines:
                if line.strip().startswith("```"):
                    if in_fence:
                        block = "\n".join(fence).strip()
                        if _looks_like_cli_example(block):
                            examples.append({"source": doc.relpath, "example": block})
                        fence = []
                    in_fence = not in_fence
                    continue
                if in_fence:
                    fence.append(line)
                elif _looks_like_cli_example(line):
                    examples.append({"source": doc.relpath, "example": line.strip()})
        return _dedupe_dicts(examples, "example")

    def _extract_snippets(self) -> list[dict[str, str]]:
        snippets: list[dict[str, str]] = []
        for doc in self.docs:
            lines = doc.text.splitlines()
            for index, line in enumerate(lines):
                lower = line.lower()
                if any(term in lower for term in CLI_TERMS) or re.search(r"\s-[A-Z][A-Z0-9]+\b", line):
                    start = max(0, index - 2)
                    end = min(len(lines), index + 3)
                    snippet = "\n".join(lines[start:end]).strip()
                    if snippet:
                        snippets.append({"source": doc.relpath, "snippet": snippet})
        return _dedupe_dicts(snippets, "snippet")


_INDEX: CliIndex | None = None


def get_index() -> CliIndex:
    global _INDEX
    if _INDEX is None:
        _INDEX = CliIndex().refresh()
    return _INDEX


def reset_index() -> None:
    global _INDEX
    _INDEX = None


def get_cli_help(command: str | None = None) -> str:
    index = get_index()
    if not command:
        sources = _format_sources(["TabularEditor/CommandLineHandler.cs"] if index.usage_text else index.sources)
        return (index.usage_text or "No CLI usage text was discovered.") + "\n\nSources:\n" + sources

    query = command.lower().strip()
    matches = [
        cmd for cmd in index.commands
        if query in cmd.lower() or any(part and part in cmd.lower() for part in query.replace("/", " ").split())
    ]
    snippet_matches = _filter_records(index.snippets, query, "snippet", limit=5)
    parts = [f"Help for {command!r}:"]
    if matches:
        parts.append("\nCommands/options:\n" + "\n".join("- " + match for match in matches[:12]))
    if snippet_matches:
        parts.append("\nMatching documented snippets:\n" + _format_records(snippet_matches, "snippet"))
    if not matches and not snippet_matches:
        parts.append("No exact documented help was found. Use list_cli_commands() for discovered usage patterns.")
    return "\n".join(parts)


def list_cli_commands() -> list[str]:
    return get_index().commands


def get_cli_examples(task: str | None = None) -> str:
    index = get_index()
    records = index.examples
    if task:
        records = _filter_records(records, task.lower(), "example", limit=20)
    if not records:
        return "No matching CLI examples were discovered."
    return _format_records(records[:20], "example")


def explain_cli_usage(task: str) -> str:
    index = get_index()
    query = task.lower()
    recommendations: list[str] = []
    sources: set[str] = set()

    def add(text: str, source: str) -> None:
        recommendations.append(text)
        sources.add(source)

    if any(word in query for word in ("deploy", "deployment", "publish")):
        add(
            "Use `TabularEditor.exe <model> -D <server> <database>` for deployment. "
            "Repo agent notes recommend `-F` for full local test replacement and `-E -W` to surface Analysis Services errors/unprocessed objects in automation.",
            "docs/agents/04-change-patterns.md",
        )
    if any(word in query for word in ("script", "c#", "csx", "bulk edit", "automation")):
        add(
            "Use `-S <script-file-or-inline-script> [...]` after loading a model. "
            "Saved reusable scripts should live under `Scripts/`, be model-agnostic, and emit CLI-visible `Info`, `Warning`, or `Error` messages.",
            "docs/agents/06-tabular-editor-csharp-scripting.md",
        )
    if any(word in query for word in ("schema", "schemacheck", "source schema")):
        add(
            "Use `TabularEditor.exe <model> -SC` to check provider data-source schema differences after source schema changes.",
            "docs/agents/04-change-patterns.md",
        )
    if any(word in query for word in ("build", "bim", "model.bim")):
        add(
            "Use `-B`, `-BIM`, or `-BUILD <output> [id]` to save the loaded model as a Model.bim file after optional scripts.",
            "TabularEditor/CommandLineHandler.cs",
        )
    if "folder" in query:
        add(
            "Use `-FOLDER <output> [id]` to save as a Tabular Editor folder structure. Avoid assuming `-F` means folder inside deploy syntax, where `-F` means full deploy.",
            "TabularEditor/CommandLineHandler.cs",
        )
    if "tmdl" in query:
        add(
            "Use `-TMDL <output> [id]` to save as a TMDL folder structure.",
            "TabularEditor/CommandLineHandler.cs",
        )
    if any(word in query for word in ("bpa", "best practice", "analyze", "analyse")):
        add(
            "Use `-A`/`-ANALYZE [rules]` for Best Practice Analyzer with model rules, or `-AX`/`-ANALYZEX [rules]` to exclude rules from model annotations.",
            "TabularEditor/CommandLineHandler.cs",
        )

    if not recommendations:
        matches = _filter_records(index.snippets, query, "snippet", limit=5)
        if matches:
            return "Relevant documented snippets:\n" + _format_records(matches, "snippet")
        return (
            "No confident workflow was found in the indexed repo docs. "
            "Call list_cli_commands() and get_cli_help() to inspect discovered syntax. "
            "Confidence: low."
        )

    return "\n\n".join(recommendations) + "\n\nSources:\n" + _format_sources(sorted(sources))


def validate_cli_command(command: str) -> dict[str, Any]:
    index = get_index()
    warnings: list[str] = []
    sources: set[str] = set()

    if not command or not command.strip():
        return _validation(False, "high", ["Command is empty."], None, [])

    unsafe = _find_unsafe_shell_syntax(command)
    if unsafe:
        return _validation(False, "high", [f"Rejected shell syntax: {unsafe}."], None, ["tools/mcp/server.py"])

    try:
        argv = _parse_command(command)
    except ValueError as exc:
        return _validation(False, "high", [str(exc)], None, ["tools/mcp/server.py"])

    if not argv:
        return _validation(False, "high", ["Command is empty after parsing."], None, [])

    entrypoint = argv[0]
    if not _is_allowed_entrypoint(entrypoint):
        suggested = _suggest_with_entrypoint(argv)
        return _validation(
            False,
            "high",
            ["Only the documented TabularEditor.exe CLI entrypoint is allowed."],
            suggested,
            ["TabularEditor/CommandLineHandler.cs"],
        )

    for arg in argv[1:]:
        if arg.startswith(("-", "/")) and _looks_like_switch(arg) and arg.upper() not in index.known_switches:
            warnings.append(f"Unknown switch not found in discovered usage: {arg}")

    path_warning = _path_safety_warning(argv[1:])
    if path_warning:
        return _validation(False, "high", [path_warning], None, ["tools/mcp/server.py"])

    if any(arg.upper() in DESTRUCTIVE_SWITCHES for arg in argv[1:]):
        warnings.append("Command includes write, script, deploy, or XMLA-generation switches; keep dry_run=true unless separately reviewed.")

    if not _has_model_source_or_help(argv[1:]):
        warnings.append("No model source or help switch was detected. The CLI may launch the UI instead of running command-line work.")

    sources.add("TabularEditor/CommandLineHandler.cs")
    if any(arg.upper() in {"-S", "-SCRIPT"} for arg in argv[1:]):
        sources.add("docs/agents/06-tabular-editor-csharp-scripting.md")
    if any(arg.upper() in {"-D", "-DEPLOY", "-SC", "-SCHEMACHECK"} for arg in argv[1:]):
        sources.add("docs/agents/04-change-patterns.md")
    if "CommandLine(" in "\n".join(index.commands):
        sources.add("TabularEditorTest/CLITests.cs")

    confidence = "high" if not warnings else "medium"
    return _validation(True, confidence, warnings, None, sorted(sources))


def run_cli_command(command: str, dry_run: bool = True) -> dict[str, Any]:
    validation = validate_cli_command(command)
    result: dict[str, Any] = {
        "command": command,
        "dry_run": dry_run,
        "valid": validation["valid"],
        "validation": validation,
        "stdout": "",
        "stderr": "",
        "exit_code": None,
    }
    if not validation["valid"]:
        result["stderr"] = "\n".join(validation["warnings"])
        return result

    argv = _parse_command(command)
    resolved = _resolve_entrypoint(argv[0])
    if resolved is not None:
        argv[0] = str(resolved)

    if dry_run:
        result["stdout"] = "Dry run only. Command was validated but not executed."
        result["command"] = _argv_to_display(argv)
        return result

    unsafe_switches = [arg for arg in argv[1:] if arg.upper() in DESTRUCTIVE_SWITCHES]
    readonly_switches = {"-?", "/?", "-H", "/H", "HELP", "-SC", "-SCHEMACHECK", "-A", "-ANALYZE", "-AX", "-ANALYZEX", "-V", "-VSTS", "-G", "-GITHUB"}
    if unsafe_switches or any(_looks_like_switch(arg) and arg.upper() not in readonly_switches for arg in argv[1:]):
        result["stderr"] = (
            "Refusing to execute non-dry-run command because it contains write/deploy/script "
            f"or unclassified switches: {', '.join(unsafe_switches) or 'unclassified switches'}."
        )
        return result

    if not Path(argv[0]).exists():
        result["stderr"] = f"TabularEditor.exe was not found at {argv[0]!r}."
        result["exit_code"] = 127
        return result

    try:
        completed = subprocess.run(
            argv,
            cwd=str(REPO_ROOT),
            capture_output=True,
            text=True,
            timeout=COMMAND_TIMEOUT_SEC,
            shell=False,
        )
    except subprocess.TimeoutExpired as exc:
        result["stdout"] = exc.stdout or ""
        result["stderr"] = (exc.stderr or "") + f"\nTimed out after {COMMAND_TIMEOUT_SEC} seconds."
        result["exit_code"] = 124
        return result
    except OSError as exc:
        result["stderr"] = str(exc)
        result["exit_code"] = 126
        return result

    result["stdout"] = completed.stdout
    result["stderr"] = completed.stderr
    result["exit_code"] = completed.returncode
    result["command"] = _argv_to_display(argv)
    return result


def inspect_model(target: str) -> dict[str, Any]:
    script = """
Output("DatabaseName=" + Model.Database.Name);
Output("TableCount=" + Model.Tables.Count);
Output("ColumnCount=" + Model.AllColumns.Count());
Output("MeasureCount=" + Model.AllMeasures.Count());
Output("RelationshipCount=" + Model.Relationships.Count);
Output("DataSourceCount=" + Model.DataSources.Count);
Output("ExpressionCount=" + Model.Expressions.Count);
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_tables(target: str) -> dict[str, Any]:
    script = """
foreach(var table in Model.Tables.OrderBy(t => t.Name))
{
    Output("TABLE|" + table.Name + "|Columns=" + table.Columns.Count + "|Measures=" + table.Measures.Count + "|Partitions=" + table.Partitions.Count + "|Hidden=" + table.IsHidden);
}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_columns(target: str, table: str | None = None) -> dict[str, Any]:
    table_filter = _cs_string(table) if table else "null"
    script = f"""
string tableFilter = {table_filter};
var tables = tableFilter == null ? Model.Tables : Model.Tables.Where(t => t.Name.Equals(tableFilter, System.StringComparison.OrdinalIgnoreCase));
foreach(var t in tables.OrderBy(t => t.Name))
{{
    foreach(var column in t.Columns.OrderBy(c => c.Name))
    {{
        Output("COLUMN|" + t.Name + "|" + column.Name + "|Type=" + column.GetType().Name + "|DataType=" + column.DataType + "|Hidden=" + column.IsHidden);
    }}
}}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_measures(target: str, table: str | None = None) -> dict[str, Any]:
    table_filter = _cs_string(table) if table else "null"
    script = f"""
string tableFilter = {table_filter};
var tables = tableFilter == null ? Model.Tables : Model.Tables.Where(t => t.Name.Equals(tableFilter, System.StringComparison.OrdinalIgnoreCase));
foreach(var t in tables.OrderBy(t => t.Name))
{{
    foreach(var measure in t.Measures.OrderBy(m => m.Name))
    {{
        Output("MEASURE|" + t.Name + "|" + measure.Name + "|Hidden=" + measure.IsHidden + "|FormatString=" + (measure.FormatString ?? ""));
    }}
}}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_relationships(target: str) -> dict[str, Any]:
    script = """
foreach(var rel in Model.Relationships)
{
    Output("RELATIONSHIP|" + rel.FromTable.Name + "|" + rel.FromColumn.Name + "|" + rel.ToTable.Name + "|" + rel.ToColumn.Name + "|Active=" + rel.IsActive + "|CrossFiltering=" + rel.CrossFilteringBehavior);
}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_partitions(target: str, table: str | None = None) -> dict[str, Any]:
    table_filter = _cs_string(table) if table else "null"
    script = f"""
System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
string tableFilter = {table_filter};
var tables = tableFilter == null ? Model.Tables : Model.Tables.Where(t => t.Name.Equals(tableFilter, System.StringComparison.OrdinalIgnoreCase));
foreach(var t in tables.OrderBy(t => t.Name))
{{
    foreach(var partition in t.Partitions.OrderBy(p => p.Name))
    {{
        var metadataProp = partition.GetType().GetProperties(flags).First(p => p.Name == "MetadataObject" && p.PropertyType.FullName == "Microsoft.AnalysisServices.Tabular.Partition");
        object metadata = metadataProp.GetValue(partition, null);
        var modeProp = metadata.GetType().GetProperty("Mode", flags);
        var sourceProp = metadata.GetType().GetProperty("Source", flags);
        object source = sourceProp == null ? null : sourceProp.GetValue(metadata, null);
        Output("PARTITION|" + t.Name + "|" + partition.Name + "|Mode=" + (modeProp == null ? "" : (modeProp.GetValue(metadata, null) ?? "").ToString()) + "|SourceType=" + (source == null ? "NULL" : source.GetType().Name));
    }}
}}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_data_sources(target: str) -> dict[str, Any]:
    script = """
foreach(var dataSource in Model.DataSources.OrderBy(d => d.Name))
{
    Output("DATASOURCE|" + dataSource.Name + "|Type=" + dataSource.GetType().Name);
}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def list_expressions(target: str) -> dict[str, Any]:
    script = """
foreach(var expression in Model.Expressions.OrderBy(e => e.Name))
{
    Output("EXPRESSION|" + expression.Name + "|Kind=" + expression.Kind + "|Length=" + (expression.Expression == null ? 0 : expression.Expression.Length));
}
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def find_objects(target: str, query: str) -> dict[str, Any]:
    script = f"""
string query = {_cs_string(query)};
foreach(var table in Model.Tables.Where(t => t.Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(t => t.Name))
    Output("TABLE|" + table.Name);
foreach(var column in Model.AllColumns.Where(c => c.Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(c => c.Table.Name).ThenBy(c => c.Name))
    Output("COLUMN|" + column.Table.Name + "|" + column.Name);
foreach(var measure in Model.AllMeasures.Where(m => m.Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(m => m.Table.Name).ThenBy(m => m.Name))
    Output("MEASURE|" + measure.Table.Name + "|" + measure.Name);
"""
    return _run_model_script(target, script, save=False, dry_run=False)


def hide_columns(target: str, column_names: list[str], table_pattern: str | None = None, dry_run: bool = True) -> dict[str, Any]:
    _require_non_empty_list(column_names, "column_names")
    table_filter = _cs_string(table_pattern) if table_pattern else "null"
    script = f"""
var names = new System.Collections.Generic.HashSet<string>({_cs_string_array(column_names)}, System.StringComparer.OrdinalIgnoreCase);
string tablePattern = {table_filter};
var changed = 0;
foreach(var table in Model.Tables)
{{
    if(tablePattern != null && table.Name.IndexOf(tablePattern, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
    foreach(var column in table.Columns.Where(c => names.Contains(c.Name)))
    {{
        if(!column.IsHidden)
        {{
            column.IsHidden = true;
            changed++;
            Output("HID_COLUMN|" + table.Name + "|" + column.Name);
        }}
    }}
}}
Output("SUMMARY|HiddenColumns=" + changed);
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def set_table_hidden(target: str, table_names: list[str], hidden: bool = True, dry_run: bool = True) -> dict[str, Any]:
    _require_non_empty_list(table_names, "table_names")
    script = f"""
var names = new System.Collections.Generic.HashSet<string>({_cs_string_array(table_names)}, System.StringComparer.OrdinalIgnoreCase);
var changed = 0;
foreach(var tableName in names)
{{
    if(!Model.Tables.Contains(tableName)) Error("Missing table: " + tableName);
    else
    {{
        var table = Model.Tables[tableName];
        if(table.IsHidden != {_cs_bool(hidden)})
        {{
            table.IsHidden = {_cs_bool(hidden)};
            changed++;
            Output("SET_TABLE_HIDDEN|" + table.Name + "|Hidden=" + table.IsHidden);
        }}
    }}
}}
Output("SUMMARY|ChangedTables=" + changed);
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def rename_table(target: str, old_name: str, new_name: str, dry_run: bool = True) -> dict[str, Any]:
    script = f"""
if(!Model.Tables.Contains({_cs_string(old_name)})) Error("Missing table: " + {_cs_string(old_name)});
else if(Model.Tables.Contains({_cs_string(new_name)})) Error("Target table already exists: " + {_cs_string(new_name)});
else
{{
    Model.Tables[{_cs_string(old_name)}].Name = {_cs_string(new_name)};
    Output("RENAMED_TABLE|" + {_cs_string(old_name)} + "|" + {_cs_string(new_name)});
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def rename_column(target: str, table: str, old_name: str, new_name: str, dry_run: bool = True) -> dict[str, Any]:
    script = f"""
if(!Model.Tables.Contains({_cs_string(table)})) Error("Missing table: " + {_cs_string(table)});
else if(!Model.Tables[{_cs_string(table)}].Columns.Contains({_cs_string(old_name)})) Error("Missing column: " + {_cs_string(table)} + "." + {_cs_string(old_name)});
else if(Model.Tables[{_cs_string(table)}].Columns.Contains({_cs_string(new_name)})) Error("Target column already exists: " + {_cs_string(table)} + "." + {_cs_string(new_name)});
else
{{
    Model.Tables[{_cs_string(table)}].Columns[{_cs_string(old_name)}].Name = {_cs_string(new_name)};
    Output("RENAMED_COLUMN|" + {_cs_string(table)} + "|" + {_cs_string(old_name)} + "|" + {_cs_string(new_name)});
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def delete_tables(target: str, table_names: list[str], dry_run: bool = True) -> dict[str, Any]:
    _require_non_empty_list(table_names, "table_names")
    script = f"""
var names = {_cs_string_array(table_names)};
foreach(var tableName in names)
{{
    if(!Model.Tables.Contains(tableName)) Error("Missing table: " + tableName);
}}
var deleted = 0;
foreach(var tableName in names)
{{
    if(Model.Tables.Contains(tableName))
    {{
        Model.Tables[tableName].Delete();
        deleted++;
        Output("DELETED_TABLE|" + tableName);
    }}
}}
Output("SUMMARY|DeletedTables=" + deleted);
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def delete_columns(target: str, table: str, column_names: list[str], dry_run: bool = True) -> dict[str, Any]:
    _require_non_empty_list(column_names, "column_names")
    script = f"""
if(!Model.Tables.Contains({_cs_string(table)})) Error("Missing table: " + {_cs_string(table)});
else
{{
    var names = {_cs_string_array(column_names)};
    foreach(var columnName in names)
        if(!Model.Tables[{_cs_string(table)}].Columns.Contains(columnName)) Error("Missing column: " + {_cs_string(table)} + "." + columnName);
    var deleted = 0;
    foreach(var columnName in names)
    {{
        if(Model.Tables[{_cs_string(table)}].Columns.Contains(columnName))
        {{
            Model.Tables[{_cs_string(table)}].Columns[columnName].Delete();
            deleted++;
            Output("DELETED_COLUMN|" + {_cs_string(table)} + "|" + columnName);
        }}
    }}
    Output("SUMMARY|DeletedColumns=" + deleted);
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def create_relationship(
    target: str,
    from_table: str,
    from_column: str,
    to_table: str,
    to_column: str,
    active: bool = True,
    cross_filtering_behavior: str = "OneDirection",
    dry_run: bool = True,
) -> dict[str, Any]:
    script = f"""
if(!Model.Tables.Contains({_cs_string(from_table)})) Error("Missing from_table: " + {_cs_string(from_table)});
else if(!Model.Tables[{_cs_string(from_table)}].Columns.Contains({_cs_string(from_column)})) Error("Missing from column: " + {_cs_string(from_table)} + "." + {_cs_string(from_column)});
else if(!Model.Tables.Contains({_cs_string(to_table)})) Error("Missing to_table: " + {_cs_string(to_table)});
else if(!Model.Tables[{_cs_string(to_table)}].Columns.Contains({_cs_string(to_column)})) Error("Missing to column: " + {_cs_string(to_table)} + "." + {_cs_string(to_column)});
else if(Model.Relationships.Any(r => r.FromTable.Name == {_cs_string(from_table)} && r.FromColumn.Name == {_cs_string(from_column)} && r.ToTable.Name == {_cs_string(to_table)} && r.ToColumn.Name == {_cs_string(to_column)}))
    Error("Relationship already exists.");
else
{{
    var rel = Model.Tables[{_cs_string(from_table)}].Columns[{_cs_string(from_column)}].RelateTo(Model.Tables[{_cs_string(to_table)}].Columns[{_cs_string(to_column)}]);
    rel.IsActive = {_cs_bool(active)};
    rel.CrossFilteringBehavior = (CrossFilteringBehavior)System.Enum.Parse(typeof(CrossFilteringBehavior), {_cs_string(cross_filtering_behavior)});
    Output("CREATED_RELATIONSHIP|" + {_cs_string(from_table)} + "|" + {_cs_string(from_column)} + "|" + {_cs_string(to_table)} + "|" + {_cs_string(to_column)});
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def delete_relationship(
    target: str,
    from_table: str,
    from_column: str,
    to_table: str,
    to_column: str,
    dry_run: bool = True,
) -> dict[str, Any]:
    script = f"""
var matches = Model.Relationships.Where(r => r.FromTable.Name == {_cs_string(from_table)} && r.FromColumn.Name == {_cs_string(from_column)} && r.ToTable.Name == {_cs_string(to_table)} && r.ToColumn.Name == {_cs_string(to_column)}).ToList();
if(matches.Count == 0) Error("Relationship not found.");
foreach(var rel in matches)
{{
    rel.Delete();
    Output("DELETED_RELATIONSHIP|" + {_cs_string(from_table)} + "|" + {_cs_string(from_column)} + "|" + {_cs_string(to_table)} + "|" + {_cs_string(to_column)});
}}
Output("SUMMARY|DeletedRelationships=" + matches.Count);
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def create_measure(
    target: str,
    table: str,
    name: str,
    expression: str,
    format_string: str | None = None,
    dry_run: bool = True,
) -> dict[str, Any]:
    format_line = f"measure.FormatString = {_cs_string(format_string)};" if format_string is not None else ""
    script = f"""
if(!Model.Tables.Contains({_cs_string(table)})) Error("Missing table: " + {_cs_string(table)});
else if(Model.Tables[{_cs_string(table)}].Measures.Contains({_cs_string(name)})) Error("Measure already exists: " + {_cs_string(table)} + "." + {_cs_string(name)});
else
{{
    var measure = Model.Tables[{_cs_string(table)}].AddMeasure({_cs_string(name)}, {_cs_string(expression)});
    {format_line}
    Output("CREATED_MEASURE|" + {_cs_string(table)} + "|" + {_cs_string(name)});
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def update_measure(
    target: str,
    table: str,
    name: str,
    expression: str | None = None,
    format_string: str | None = None,
    hidden: bool | None = None,
    dry_run: bool = True,
) -> dict[str, Any]:
    updates: list[str] = []
    if expression is not None:
        updates.append(f"measure.Expression = {_cs_string(expression)};")
    if format_string is not None:
        updates.append(f"measure.FormatString = {_cs_string(format_string)};")
    if hidden is not None:
        updates.append(f"measure.IsHidden = {_cs_bool(hidden)};")
    if not updates:
        raise ValueError("At least one of expression, format_string, or hidden must be provided.")
    script = f"""
if(!Model.Tables.Contains({_cs_string(table)})) Error("Missing table: " + {_cs_string(table)});
else if(!Model.Tables[{_cs_string(table)}].Measures.Contains({_cs_string(name)})) Error("Missing measure: " + {_cs_string(table)} + "." + {_cs_string(name)});
else
{{
    var measure = Model.Tables[{_cs_string(table)}].Measures[{_cs_string(name)}];
    {" ".join(updates)}
    Output("UPDATED_MEASURE|" + {_cs_string(table)} + "|" + {_cs_string(name)});
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def delete_measures(target: str, table: str, measure_names: list[str], dry_run: bool = True) -> dict[str, Any]:
    _require_non_empty_list(measure_names, "measure_names")
    script = f"""
if(!Model.Tables.Contains({_cs_string(table)})) Error("Missing table: " + {_cs_string(table)});
else
{{
    var names = {_cs_string_array(measure_names)};
    foreach(var measureName in names)
        if(!Model.Tables[{_cs_string(table)}].Measures.Contains(measureName)) Error("Missing measure: " + {_cs_string(table)} + "." + measureName);
    var deleted = 0;
    foreach(var measureName in names)
    {{
        if(Model.Tables[{_cs_string(table)}].Measures.Contains(measureName))
        {{
            Model.Tables[{_cs_string(table)}].Measures[measureName].Delete();
            deleted++;
            Output("DELETED_MEASURE|" + {_cs_string(table)} + "|" + measureName);
        }}
    }}
    Output("SUMMARY|DeletedMeasures=" + deleted);
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def copy_measures(
    target: str,
    source_table: str,
    target_table: str,
    measure_names: list[str] | None = None,
    dry_run: bool = True,
) -> dict[str, Any]:
    names = _cs_string_array(measure_names) if measure_names else "null"
    script = f"""
if(!Model.Tables.Contains({_cs_string(source_table)})) Error("Missing source table: " + {_cs_string(source_table)});
else if(!Model.Tables.Contains({_cs_string(target_table)})) Error("Missing target table: " + {_cs_string(target_table)});
else
{{
    string[] requested = {names};
    var source = Model.Tables[{_cs_string(source_table)}];
    var destination = Model.Tables[{_cs_string(target_table)}];
    var measures = requested == null ? source.Measures.ToList() : requested.Select(n => source.Measures.Contains(n) ? source.Measures[n] : null).Where(m => m != null).ToList();
    if(requested != null)
        foreach(var name in requested)
            if(!source.Measures.Contains(name)) Error("Missing measure: " + source.Name + "." + name);
    var copied = 0;
    foreach(var measure in measures)
    {{
        var clone = measure.Clone(measure.Name, true, destination);
        clone.Expression = measure.Expression;
        copied++;
        Output("COPIED_MEASURE|" + source.Name + "|" + destination.Name + "|" + clone.Name);
    }}
    Output("SUMMARY|CopiedMeasures=" + copied);
}}
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def replace_tables_with_existing_tables(
    target: str,
    mapping: dict[str, str],
    remove_helper_date_tables: bool = True,
    copy_old_measures: bool = True,
    recreate_missing_relationships: bool = True,
    hide_metadata_columns: bool = True,
    dry_run: bool = True,
) -> dict[str, Any]:
    if not mapping:
        raise ValueError("mapping must contain at least one old-table to new-table pair.")
    entries = "\n".join(f'pairs.Add({_cs_string(old)}, {_cs_string(new)});' for old, new in mapping.items())
    script = f"""
var pairs = new System.Collections.Generic.Dictionary<string,string>(System.StringComparer.OrdinalIgnoreCase);
{entries}
foreach(var pair in pairs)
{{
    if(!Model.Tables.Contains(pair.Key)) Error("Missing old table: " + pair.Key);
    if(!Model.Tables.Contains(pair.Value)) Error("Missing replacement table: " + pair.Value);
}}
var clonedMeasures = 0;
if({_cs_bool(copy_old_measures)})
{{
    foreach(var pair in pairs)
    {{
        var oldTable = Model.Tables[pair.Key];
        var newTable = Model.Tables[pair.Value];
        foreach(var measure in oldTable.Measures.ToList())
        {{
            var clone = measure.Clone(measure.Name, true, newTable);
            clone.Expression = measure.Expression;
            clonedMeasures++;
            Output("CLONED_MEASURE|" + oldTable.Name + "|" + newTable.Name + "|" + clone.Name);
        }}
    }}
}}
var recreatedRelationships = 0;
if({_cs_bool(recreate_missing_relationships)})
{{
    foreach(var rel in Model.Relationships.ToList())
    {{
        if(pairs.ContainsKey(rel.FromTable.Name) && pairs.ContainsKey(rel.ToTable.Name))
        {{
            var newFrom = Model.Tables[pairs[rel.FromTable.Name]];
            var newTo = Model.Tables[pairs[rel.ToTable.Name]];
            if(newFrom.Columns.Contains(rel.FromColumn.Name) && newTo.Columns.Contains(rel.ToColumn.Name))
            {{
                var exists = Model.Relationships.Any(r => r.FromTable == newFrom && r.ToTable == newTo && r.FromColumn.Name == rel.FromColumn.Name && r.ToColumn.Name == rel.ToColumn.Name);
                if(!exists)
                {{
                    var newRel = SingleColumnRelationship.CreateNew(Model);
                    newRel.FromColumn = newFrom.Columns[rel.FromColumn.Name];
                    newRel.ToColumn = newTo.Columns[rel.ToColumn.Name];
                    newRel.IsActive = rel.IsActive;
                    newRel.CrossFilteringBehavior = rel.CrossFilteringBehavior;
                    newRel.JoinOnDateBehavior = rel.JoinOnDateBehavior;
                    newRel.SecurityFilteringBehavior = rel.SecurityFilteringBehavior;
                    recreatedRelationships++;
                    Output("RECREATED_RELATIONSHIP|" + rel.FromTable.Name + "|" + rel.FromColumn.Name + "|" + rel.ToTable.Name + "|" + rel.ToColumn.Name);
                }}
            }}
            else Error("Cannot recreate relationship due to missing column: " + rel.FromTable.Name + "." + rel.FromColumn.Name + " -> " + rel.ToTable.Name + "." + rel.ToColumn.Name);
        }}
    }}
}}
var deletedOldTables = 0;
foreach(var pair in pairs.ToList())
{{
    Model.Tables[pair.Key].Delete();
    deletedOldTables++;
    Output("DELETED_OLD_TABLE|" + pair.Key);
}}
var deletedHelperTables = 0;
if({_cs_bool(remove_helper_date_tables)})
{{
    foreach(var table in Model.Tables.Where(t => t.Name.StartsWith("LocalDateTable_") || t.Name.StartsWith("DateTableTemplate_")).ToList())
    {{
        table.Delete();
        deletedHelperTables++;
        Output("DELETED_HELPER_TABLE|" + table.Name);
    }}
}}
var renamedTables = 0;
foreach(var pair in pairs.ToList())
{{
    var table = Model.Tables[pair.Value];
    table.Name = pair.Key;
    renamedTables++;
    Output("RENAMED_TABLE|" + pair.Value + "|" + pair.Key);
}}
var hiddenMetadataColumns = 0;
if({_cs_bool(hide_metadata_columns)})
{{
    var metadataNames = new[] {{ "Valid From", "Valid To", "Lineage Key" }};
    foreach(var table in Model.Tables)
    {{
        foreach(var column in table.Columns.Where(c => metadataNames.Contains(c.Name, System.StringComparer.OrdinalIgnoreCase)))
        {{
            if(!column.IsHidden)
            {{
                column.IsHidden = true;
                hiddenMetadataColumns++;
            }}
        }}
    }}
}}
Output("SUMMARY|ClonedMeasures=" + clonedMeasures + "|RecreatedRelationships=" + recreatedRelationships + "|DeletedOldTables=" + deletedOldTables + "|DeletedHelperTables=" + deletedHelperTables + "|RenamedTables=" + renamedTables + "|HiddenMetadataColumns=" + hiddenMetadataColumns + "|FinalTables=" + Model.Tables.Count + "|FinalRelationships=" + Model.Relationships.Count);
"""
    return _run_model_script(target, script, save=True, dry_run=dry_run)


def create_mcp() -> Any:
    try:
        from fastmcp import FastMCP
    except ImportError:
        from mcp.server.fastmcp import FastMCP

    mcp = FastMCP("tabulareditor-cli")
    for tool in (
        get_cli_help,
        list_cli_commands,
        get_cli_examples,
        explain_cli_usage,
        validate_cli_command,
        run_cli_command,
        inspect_model,
        list_tables,
        list_columns,
        list_measures,
        list_relationships,
        list_partitions,
        list_data_sources,
        list_expressions,
        find_objects,
        hide_columns,
        set_table_hidden,
        rename_table,
        rename_column,
        delete_tables,
        delete_columns,
        create_relationship,
        delete_relationship,
        create_measure,
        update_measure,
        delete_measures,
        copy_measures,
        replace_tables_with_existing_tables,
    ):
        mcp.tool()(tool)
    return mcp


def main() -> None:
    logging.basicConfig(level=os.environ.get("TE_CLI_MCP_LOG", "WARNING"), stream=sys.stderr)
    create_mcp().run(transport="stdio")


def _run_model_script(target: str, script: str, save: bool, dry_run: bool) -> dict[str, Any]:
    try:
        target_args = _parse_target_args(target)
    except ValueError as exc:
        return {
            "valid": False,
            "dry_run": dry_run,
            "save": save,
            "command": "",
            "script": script.strip(),
            "stdout": "",
            "stderr": str(exc),
            "exit_code": None,
        }

    display_script = "<generated-script.csx>"
    display_argv = ["TabularEditor.exe", *target_args, "-S", display_script]
    if save:
        display_argv.append("-D")

    result: dict[str, Any] = {
        "valid": True,
        "dry_run": dry_run,
        "save": save,
        "command": _argv_to_display(display_argv),
        "script": script.strip(),
        "stdout": "",
        "stderr": "",
        "exit_code": None,
    }
    if dry_run:
        result["stdout"] = "Dry run only. Generated script was not executed."
        return result

    exe = _resolve_entrypoint("TabularEditor.exe")
    if exe is None or not exe.exists():
        result["stderr"] = f"TabularEditor.exe was not found at {exe!s}."
        result["exit_code"] = 127
        return result

    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    script_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            suffix=".csx",
            prefix="te-mcp-",
            dir=RUNTIME_DIR,
            delete=False,
        ) as handle:
            handle.write(script)
            script_path = Path(handle.name)

        argv = [str(exe), *target_args, "-S", str(script_path)]
        if save:
            argv.append("-D")
        completed = subprocess.run(
            argv,
            cwd=str(REPO_ROOT),
            capture_output=True,
            text=True,
            timeout=COMMAND_TIMEOUT_SEC,
            shell=False,
        )
        result["command"] = _argv_to_display([str(exe), *target_args, "-S", str(script_path), *(["-D"] if save else [])])
        result["stdout"] = completed.stdout
        result["stderr"] = completed.stderr
        result["exit_code"] = completed.returncode
        return result
    except subprocess.TimeoutExpired as exc:
        result["stdout"] = exc.stdout or ""
        result["stderr"] = (exc.stderr or "") + f"\nTimed out after {COMMAND_TIMEOUT_SEC} seconds."
        result["exit_code"] = 124
        return result
    except OSError as exc:
        result["stderr"] = str(exc)
        result["exit_code"] = 126
        return result
    finally:
        if script_path is not None:
            try:
                script_path.unlink(missing_ok=True)
            except OSError as exc:
                LOG.warning("Unable to delete runtime script %s: %s", script_path, exc)


def _parse_target_args(target: str) -> list[str]:
    if not target or not target.strip():
        raise ValueError("target is empty. Use a model file/folder, 'server database', or '-L [name]'.")
    unsafe = _find_unsafe_shell_syntax(target)
    if unsafe:
        raise ValueError(f"Rejected shell syntax in target: {unsafe}.")
    args = _parse_command(target)
    if not args:
        raise ValueError("target is empty after parsing.")
    allowed_target_switches = {"-L", "-LOCAL"}
    for index, arg in enumerate(args):
        if _looks_like_switch(arg) and arg.upper() not in allowed_target_switches:
            raise ValueError(f"Target must not include CLI operation switches: {arg}")
        if arg.upper() in allowed_target_switches and index != 0:
            raise ValueError("-L/-LOCAL is only valid as the first target token.")
    path_warning = _path_safety_warning(args)
    if path_warning:
        raise ValueError(path_warning)
    return args


def _require_non_empty_list(values: list[str], name: str) -> None:
    if not values:
        raise ValueError(f"{name} must contain at least one value.")
    if any(not value or not value.strip() for value in values):
        raise ValueError(f"{name} must not contain empty values.")


def _cs_bool(value: bool) -> str:
    return "true" if value else "false"


def _cs_string(value: str) -> str:
    import json

    return json.dumps(value)


def _cs_string_array(values: list[str] | None) -> str:
    if values is None:
        return "null"
    return "new[] { " + ", ".join(_cs_string(value) for value in values) + " }"


def _looks_like_cli_example(text: str) -> bool:
    lower = text.lower()
    return "tabulareditor.exe" in lower or "commandline(" in lower or "start-process -filepath" in lower


def _filter_records(records: list[dict[str, str]], query: str, field: str, limit: int) -> list[dict[str, str]]:
    words = [word for word in re.split(r"[^a-zA-Z0-9_.-]+", query.lower()) if word]
    if not words:
        return records[:limit]
    scored: list[tuple[int, dict[str, str]]] = []
    for record in records:
        text = (record.get(field, "") + " " + record.get("source", "")).lower()
        score = sum(1 for word in words if word in text)
        if score:
            scored.append((score, record))
    scored.sort(key=lambda item: item[0], reverse=True)
    return [record for _, record in scored[:limit]]


def _format_records(records: list[dict[str, str]], field: str) -> str:
    return "\n\n".join(f"Source: {record['source']}\n{record[field]}" for record in records)


def _format_sources(sources: list[str]) -> str:
    return "\n".join("- " + source for source in sources) if sources else "- No sources discovered."


def _dedupe(items: list[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for item in items:
        if item not in seen:
            seen.add(item)
            result.append(item)
    return result


def _dedupe_dicts(items: list[dict[str, str]], key: str) -> list[dict[str, str]]:
    seen: set[tuple[str, str]] = set()
    result: list[dict[str, str]] = []
    for item in items:
        marker = (item.get("source", ""), item.get(key, ""))
        if marker not in seen:
            seen.add(marker)
            result.append(item)
    return result


def _validation(valid: bool, confidence: str, warnings: list[str], suggested_command: str | None, sources: list[str]) -> dict[str, Any]:
    return {
        "valid": valid,
        "confidence": confidence,
        "warnings": warnings,
        "suggested_command": suggested_command,
        "sources": sources,
    }


def _find_unsafe_shell_syntax(command: str) -> str | None:
    in_quote: str | None = None
    index = 0
    while index < len(command):
        char = command[index]
        if char in {"'", '"'}:
            in_quote = None if in_quote == char else char if in_quote is None else in_quote
        if in_quote is None:
            two = command[index:index + 2]
            if two in {"&&", "||", ">>"}:
                return two
            if char in {"|", "&", ";", ">", "<", "`", "\n", "\r"}:
                return char
        index += 1
    return None


def _parse_command(command: str) -> list[str]:
    try:
        argv = shlex.split(command, posix=False)
    except ValueError as exc:
        raise ValueError(f"Unable to parse command: {exc}") from exc
    return [_strip_quotes(arg) for arg in argv]


def _strip_quotes(value: str) -> str:
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def _is_allowed_entrypoint(entrypoint: str) -> bool:
    path = Path(entrypoint)
    if path.name.lower() != "tabulareditor.exe":
        return False
    if path.parent == Path(".") or str(path.parent) == "":
        return True
    try:
        resolved = (REPO_ROOT / path).resolve() if not path.is_absolute() else path.resolve()
        resolved.relative_to(REPO_ROOT.resolve())
        return True
    except ValueError:
        return False


def _resolve_entrypoint(entrypoint: str) -> Path | None:
    path = Path(entrypoint)
    if path.parent == Path(".") or str(path.parent) == "":
        candidate = REPO_ROOT / "TabularEditor" / "bin" / "Debug" / "TabularEditor.exe"
        return candidate
    return (REPO_ROOT / path).resolve() if not path.is_absolute() else path.resolve()


def _suggest_with_entrypoint(argv: list[str]) -> str:
    tail = " ".join(shlex.quote(arg) for arg in argv[1:])
    return ("TabularEditor.exe " + tail).strip()


def _looks_like_switch(arg: str) -> bool:
    return bool(re.match(r"^[-/][A-Za-z?][A-Za-z0-9?]*$", arg)) or arg.upper() == "HELP"


def _path_safety_warning(args: list[str]) -> str | None:
    for arg in args:
        if _looks_like_switch(arg) or _looks_like_connection_or_server(arg):
            continue
        if not _looks_like_path(arg):
            continue
        try:
            path = Path(arg)
            resolved = (REPO_ROOT / path).resolve() if not path.is_absolute() else path.resolve()
            resolved.relative_to(REPO_ROOT.resolve())
        except (OSError, ValueError):
            return f"Path-like argument is outside the repository or invalid: {arg}"
    return None


def _looks_like_connection_or_server(arg: str) -> bool:
    lower = arg.lower()
    if re.match(r"^[A-Za-z]:[\\/]", arg):
        return False
    return (
        lower.startswith("localhost")
        or "data source=" in lower
        or "provider=" in lower
        or "\\" in arg and not any(arg.lower().endswith(ext) for ext in _PATH_EXTENSIONS)
    )


_PATH_EXTENSIONS = (
    ".bim",
    ".csx",
    ".json",
    ".tmdl",
    ".tmd",
    ".txt",
    ".trx",
    ".xmla",
    ".tmsl",
)


def _looks_like_path(arg: str) -> bool:
    lower = arg.lower()
    return (
        any(separator in arg for separator in ("/", "\\"))
        or bool(re.match(r"^[A-Za-z]:", arg))
        or lower.endswith(_PATH_EXTENSIONS)
    )


def _has_model_source_or_help(args: list[str]) -> bool:
    if any(arg.upper() in {"-?", "/?", "-H", "/H", "HELP"} for arg in args):
        return True
    if not args:
        return False
    if args[0].upper() in {"-L", "-LOCAL"}:
        return True
    return not _looks_like_switch(args[0])


def _argv_to_display(argv: list[str]) -> str:
    return subprocess.list2cmdline(argv)


if __name__ == "__main__":
    main()
