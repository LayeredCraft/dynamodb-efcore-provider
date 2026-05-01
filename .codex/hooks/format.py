#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# ///

from __future__ import annotations

import json
import re
import subprocess
import sys
import traceback
from datetime import datetime, timezone
from pathlib import Path
from time import perf_counter

DOTSETTINGS_FILE_NAME = "EntityFrameworkCore.DynamoDb.sln.DotSettings"


def _read_hook_input(raw_input: str) -> dict:
    if not raw_input.strip():
        return {}

    payload = json.loads(raw_input)
    if not isinstance(payload, dict):
        raise TypeError("Hook input JSON must be an object.")

    return payload


def _edited_files(payload: dict) -> list[str]:
    command = payload.get("tool_input", {}).get("command", "")
    files: list[str] = []

    for line in command.splitlines():
        for prefix in ("*** Update File: ", "*** Add File: "):
            if line.startswith(prefix):
                files.append(line.removeprefix(prefix))

    return files


def _logs_dir() -> Path:
    return Path(__file__).resolve().parents[1] / "logs"


def _log_file_name(session_id: object) -> str:
    if not isinstance(session_id, str) or not session_id.strip():
        return "unknown-session.jsonl"

    safe_session_id = re.sub(r"[^A-Za-z0-9_.-]", "_", session_id.strip())
    return f"{safe_session_id}.jsonl"


def _write_log_entry(session_id: object, entry: dict) -> None:
    logs_dir = _logs_dir()
    logs_dir.mkdir(parents=True, exist_ok=True)
    log_path = logs_dir / _log_file_name(session_id)

    with log_path.open("a", encoding="utf-8") as log_file:
        log_file.write(json.dumps(entry, ensure_ascii=False, sort_keys=True))
        log_file.write("\n")


def _absolute_edited_file(cwd: object, edited_file: str) -> str:
    edited_path = Path(edited_file)
    if edited_path.is_absolute():
        return str(edited_path)

    if isinstance(cwd, str) and cwd.strip():
        return str((Path(cwd) / edited_path).resolve())

    return str(edited_path.resolve())


def _run_formatter(cwd: object, edited_file: str) -> dict:
    started_at = perf_counter()
    absolute_edited_file = _absolute_edited_file(cwd, edited_file)
    file_extension = Path(absolute_edited_file).suffix.lower()

    match file_extension:
        case ".cs" | ".csx" | ".csproj" | ".props":
            command = [
                "dotnet",
                "tool",
                "run",
                "jb",
                "cleanupcode",
                "--profile=Built-in: Reformat Code",
                f"--include={absolute_edited_file}",
                f"--settings={DOTSETTINGS_FILE_NAME}",
                "--verbosity=FATAL",
            ]
            formatter = "jb cleanupcode"
        case ".md":
            command = ["uv", "run", "mdformat", absolute_edited_file]
            formatter = "mdformat"
        case _:
            return {
                "file": edited_file,
                "absolute_file": absolute_edited_file,
                "formatter": None,
                "skipped": True,
                "reason": f"Unsupported file extension: {file_extension}",
                "duration_seconds": round(perf_counter() - started_at, 6),
            }

    result = subprocess.run(
        command,
        cwd=cwd if isinstance(cwd, str) and cwd.strip() else None,
        capture_output=True,
        text=True,
    )

    return {
        "file": edited_file,
        "absolute_file": absolute_edited_file,
        "formatter": formatter,
        "command": command,
        "exit_code": result.returncode,
        "stdout": result.stdout,
        "stderr": result.stderr,
        "skipped": False,
        "duration_seconds": round(perf_counter() - started_at, 6),
    }


def main() -> int:
    hook_started_at = perf_counter()
    raw_input = sys.stdin.read()
    cwd = None
    session_id = None

    try:
        payload = _read_hook_input(raw_input)
        cwd = payload.get("cwd")
        session_id = payload.get("session_id")
        edited_files = _edited_files(payload)

        entry = {
            "logged_at": datetime.now(timezone.utc).isoformat(),
            "cwd": cwd,
            "session_id": session_id,
            "turn_id": payload.get("turn_id"),
            "tool_use_id": payload.get("tool_use_id"),
            "edited_files": edited_files,
            "format_results": [_run_formatter(cwd, edited_file) for edited_file in edited_files],
            "duration_seconds": round(perf_counter() - hook_started_at, 6),
        }
    except Exception as error:
        entry = {
            "logged_at": datetime.now(timezone.utc).isoformat(),
            "cwd": cwd,
            "session_id": session_id,
            "error": {
                "type": type(error).__name__,
                "message": str(error),
                "traceback": traceback.format_exc(),
            },
            "raw_input": raw_input,
            "duration_seconds": round(perf_counter() - hook_started_at, 6),
        }

    _write_log_entry(session_id, entry)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
