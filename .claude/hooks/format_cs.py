#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.14"
# ///

import json
import sys
import subprocess


def main():
    try:
        # Read JSON input from stdin
        input_data = json.load(sys.stdin)

        cwd = input_data["cwd"]
        eddited_input = input_data["tool_input"]["file_path"]

        print(f"Running code cleanup on: '{eddited_input}' in directory: '{cwd}'")

        print("======================================")

        result = subprocess.run(
            [
                "dotnet",
                "tool",
                "run",
                "jb",
                "cleanupcode",
                '--profile=Built-in: Reformat Code',
                f'--include={eddited_input}',
                '--settings=EntityFrameworkCore.DynamoDb.sln.DotSettings',
            ],
            cwd=cwd,
            capture_output=True,
            text=True,
        )

        print(result.stdout)

        print("======================================")

        sys.exit(0)

    except json.JSONDecodeError:
        # Handle JSON decode errors gracefully
        sys.exit(0)
    except Exception:
        # Exit cleanly on any other error
        sys.exit(0)


if __name__ == "__main__":
    print("Running format_cs.py hook...")
    main()
