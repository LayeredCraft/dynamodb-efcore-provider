#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.14"
# ///

import json
import os
import sys
import subprocess

DOTSETTINGS_FILE_NAME = "EntityFrameworkCore.DynamoDb.sln.DotSettings"


def main():
    try:
        # Read JSON input from stdin
        input_data = json.load(sys.stdin)

        cwd = input_data["cwd"]
        eddited_input = input_data["tool_input"]["file_path"]
        _, ext = os.path.splitext(eddited_input)

        print(f"Running code cleanup on: '{eddited_input}' in directory: '{cwd}'")

        match ext.lower():
            case ".cs":
                csharp(cwd, eddited_input)
            case ".md":
                markdown(cwd, eddited_input)
            case _:
                print(f"Skipping unsupported file type: '{ext}'")

        sys.exit(0)

    except json.JSONDecodeError:
        # Handle JSON decode errors gracefully
        sys.exit(0)
    except Exception:
        # Exit cleanly on any other error
        sys.exit(0)


def csharp(cwd: str, eddited_input: str) -> None:
    print("======================================")

    print("Running C# code cleanup...")

    result = subprocess.run(
        [
            "dotnet",
            "tool",
            "run",
            "jb",
            "cleanupcode",
            "--profile=Built-in: Reformat Code",
            f"--include={eddited_input}",
            f"--settings={DOTSETTINGS_FILE_NAME}",
        ],
        cwd=cwd,
        capture_output=True,
        text=True,
    )

    print(result.stdout)

    print("======================================")


def markdown(cwd: str, eddited_input: str) -> None:
    print("======================================")

    print("Running Markdown code cleanup...")

    result = subprocess.run(
        [
            "uv",
            "run",
            "mdformat",
            eddited_input,
        ],
        cwd=cwd,
        capture_output=True,
        text=True,
    )

    print(result.stdout)

    print("======================================")


if __name__ == "__main__":
    print("Running format_cs.py hook...")
    main()
