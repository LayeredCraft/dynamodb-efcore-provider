#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["loguru>=0.7.3"]
# ///

from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path
from time import perf_counter

DOTSETTINGS_FILE_NAME = "EntityFrameworkCore.DynamoDb.sln.DotSettings"
DEFAULT_LOG_LEVEL = "ERROR"
DEFAULT_LOG_RETENTION = "14 days"
DEFAULT_LOG_ROTATION = "10 MB"
SUPPORTED_EXTENSIONS = {".cs", ".csx", ".csproj", ".props", ".md"}


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


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _log_file_name(session_id: object) -> str:
    if not isinstance(session_id, str) or not session_id.strip():
        return "unknown-session.jsonl"

    safe_session_id = re.sub(r"[^A-Za-z0-9_.-]", "_", session_id.strip())
    return f"{safe_session_id}.jsonl"


def _logger_for(session_id: object):
    from loguru import logger

    logs_dir = _logs_dir()
    logs_dir.mkdir(parents=True, exist_ok=True)
    log_path = logs_dir / _log_file_name(session_id)

    logger.remove()
    logger.add(
        log_path,
        level=DEFAULT_LOG_LEVEL,
        retention=DEFAULT_LOG_RETENTION,
        rotation=DEFAULT_LOG_ROTATION,
        serialize=True,
        enqueue=False,
    )

    return logger


def _should_log(level: str) -> bool:
    from loguru import logger

    return logger.level(level).no >= logger.level(DEFAULT_LOG_LEVEL).no


def _log_entry(session_id: object, level: str, message: str, entry: dict) -> None:
    if not _should_log(level):
        return

    logger = _logger_for(session_id)
    logger.bind(**entry).log(level, message)


def _log_exception(session_id: object, message: str, entry: dict) -> None:
    if not _should_log("ERROR"):
        return

    logger = _logger_for(session_id)
    logger.bind(**entry).exception(message)


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

    repo_root = _repo_root()

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
        cwd=repo_root,
        capture_output=True,
        text=True,
    )

    format_result = {
        "file": edited_file,
        "absolute_file": absolute_edited_file,
        "formatter": formatter,
        "command": command,
        "exit_code": result.returncode,
        "skipped": False,
        "duration_seconds": round(perf_counter() - started_at, 6),
    }

    if result.returncode != 0:
        format_result["stdout"] = result.stdout
        format_result["stderr"] = result.stderr

    return format_result


def _has_supported_files(edited_files: list[str], cwd: object) -> bool:
    return any(
        Path(_absolute_edited_file(cwd, edited_file)).suffix.lower() in SUPPORTED_EXTENSIONS
        for edited_file in edited_files
    )


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

        if not _has_supported_files(edited_files, cwd) and not _should_log("INFO"):
            return 0

        format_results = [_run_formatter(cwd, edited_file) for edited_file in edited_files]
        level = "ERROR" if any(
            result.get("exit_code", 0) != 0 for result in format_results) else "INFO"

        entry = {
            "cwd": cwd,
            "session_id": session_id,
            "turn_id": payload.get("turn_id"),
            "tool_use_id": payload.get("tool_use_id"),
            "edited_files": edited_files,
            "format_results": format_results,
            "duration_seconds": round(perf_counter() - hook_started_at, 6),
        }
        message = "Failed to format edited files" if level == "ERROR" else "Formatted edited files"
        _log_entry(session_id, level, message, entry)
    except Exception as error:
        entry = {
            "cwd": cwd,
            "session_id": session_id,
            "error": {
                "type": type(error).__name__,
                "message": str(error),
            },
            "raw_input": raw_input,
            "duration_seconds": round(perf_counter() - hook_started_at, 6),
        }
        _log_exception(session_id, "Codex format hook failed", entry)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
