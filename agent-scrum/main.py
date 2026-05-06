import json
import os
from pyexpat.errors import messages
import subprocess
from pathlib import Path
from typing import Any

from dotenv import load_dotenv
from litellm import completion


load_dotenv(override=True)

api_key = os.getenv("OPENROUTER_API_KEY")

if not api_key:
    raise RuntimeError("OPENROUTER_API_KEY is missing")

print(f"Using OpenRouter key: {api_key[:12]}...{api_key[-6:]}")


PROJECT_DIR = Path(os.getenv("PROJECT_DIR", "workspace"))
PROJECT_DIR.mkdir(parents=True, exist_ok=True)


def get_llm_response(messages: list[dict[str, str]]) -> str:
    response = completion(
        model=os.getenv("LLM_MODEL"),
        api_key=os.getenv("OPENROUTER_API_KEY"),
        api_base=os.getenv("LLM_BASE_URL"),
        messages=messages,
        temperature=0.2,
    )

    return response["choices"][0]["message"]["content"]


def list_files(path: str = ".") -> str:
    target = PROJECT_DIR / path

    if not target.exists():
        return f"Path does not exist: {path}"

    ignored_dirs = {
        ".git",
        ".venv",
        "venv",
        "__pycache__",
        "bin",
        "obj",
        "node_modules",
        ".vs",
        ".idea",
        ".vscode",
        "dist",
        "build",
    }

    result = []

    for item in target.rglob("*"):
        if any(part in ignored_dirs for part in item.parts):
            continue

        if item.is_file():
            result.append(str(item.relative_to(PROJECT_DIR)))

        if len(result) >= 200:
            result.append("...file list truncated...")
            break

    return "\n".join(result) if result else "No files found."


def read_file(path: str) -> str:
    target = PROJECT_DIR / path

    if not target.exists():
        return f"File does not exist: {path}"

    ignored_suffixes = {
        ".dll",
        ".exe",
        ".pdb",
        ".cache",
        ".assets",
        ".lock",
        ".deps.json",
        ".runtimeconfig.json",
    }

    if target.suffix.lower() in ignored_suffixes:
        return f"Refused to read generated/binary file: {path}"

    content = target.read_text(encoding="utf-8", errors="replace")

    max_chars = 12000
    if len(content) > max_chars:
        return content[:max_chars] + "\n\n...file truncated..."

    return content


def write_file(path: str, content: str) -> str:
    target = PROJECT_DIR / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")

    return f"File written: {path}"


def run_command(command: str, path: str = ".") -> str:
    cwd = PROJECT_DIR / path

    if Path(path).is_absolute():
        cwd = Path(path)

    if not cwd.exists():
        return f"Command path does not exist: {cwd}"

    result = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        shell=True,
        timeout=300,
    )

    output = ""

    if result.stdout:
        output += f"STDOUT:\n{result.stdout[-8000:]}\n"

    if result.stderr:
        output += f"STDERR:\n{result.stderr[-8000:]}\n"

    output += f"Exit code: {result.returncode}"

    return output


def run_tool(tool: str, args: dict[str, Any]) -> str:
    if tool == "list_files":
        return list_files(args.get("path", "."))

    if tool == "read_file":
        return read_file(args["path"])

    if tool == "write_file":
        return write_file(args["path"], args["content"])

    if tool == "run_command":
        return run_command(args["command"])

    return f"Unknown tool: {tool}"


SYSTEM_PROMPT = """
You are an autonomous coding agent.

You can use these tools:
1. list_files
2. read_file
3. write_file
4. run_command

You must respond with valid JSON only.

JSON format:
{
  "thought": "short reasoning",
  "actions": [
    {
      "tool": "tool_name",
      "args": {}
    }
  ],
  "done": false,
  "final_answer": ""
}

Rules:
- Use tools to inspect, create, update, and verify code.
- Do not only explain code.
- Actually create files using write_file.
- Use run_command to run builds/tests.
- For .NET apps, run dotnet build.
- If a command fails, fix the code and run again.
- When finished, set done=true and include summary in final_answer.
- Return JSON only. No markdown.
- Do not set done=true if actions is not empty.
- If you need to modify files, return actions first with done=false.
- Only return done=true after tool results confirm the files were written and build succeeded.
- Always use relative paths from PROJECT_DIR. Do not use absolute Windows paths.
"""


def parse_json_response(text: str) -> dict[str, Any]:
    text = text.strip()

    if text.startswith("```json"):
        text = text.removeprefix("```json").removesuffix("```").strip()

    if text.startswith("```"):
        text = text.removeprefix("```").removesuffix("```").strip()

    return json.loads(text)


def run_agent(user_prompt: str, max_steps: int = 10) -> str:
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {
            "role": "user",
            "content": (
                f"User request:\n{user_prompt}\n\n"
                f"Workspace path:\n{PROJECT_DIR}\n\n"
                "Start by inspecting files, then perform the task."
            ),
        },
    ]

    def trim_messages(messages: list[dict[str, str]]) -> list[dict[str, str]]:
        if len(messages) <= 8:
            return messages

        return [
            messages[0],  # system prompt
            messages[1],  # original user prompt
            *messages[-6:]
        ]

    for step in range(1, max_steps + 1):
        print(f"\n--- Agent step {step} ---")
 
        messages = trim_messages(messages)
        raw_response = get_llm_response(messages)


        print(raw_response)

        try:
            agent_response = parse_json_response(raw_response)
        except Exception as exc:
            messages.append(
                {
                    "role": "user",
                    "content": f"Your previous response was invalid JSON. Error: {exc}. Return valid JSON only.",
                }
            )
            continue

        actions = agent_response.get("actions", [])

        # Only finish immediately when there are no actions left to run.
        # Some models incorrectly return done=true together with actions.
        # In that case, we still execute the actions first.
        if agent_response.get("done") and not actions:
            return agent_response.get("final_answer", "Done.")

        if agent_response.get("done"):
            return agent_response.get("final_answer", "Done.")

        tool_results = []

        for action in actions:
            tool = action.get("tool")
            args = action.get("args", {})

            print(f"\nRunning tool: {tool}")
            result = run_tool(tool, args)
            print(result)

            tool_results.append(
                {
                    "tool": tool,
                    "args": args,
                    "result": result,
                }
            )

        messages.append(
            {
                "role": "assistant",
                "content": raw_response,
            }
        )

        messages.append(
            {
                "role": "user",
                "content": (
                    "Tool results:\n"
                    + json.dumps(tool_results, indent=2)
                    + "\n\nContinue. If the task is complete, return done=true."
                ),
            }
        )

    return "Agent stopped because max_steps was reached."


def main() -> None:
    prompt = input("Enter coding request: ").strip()

    if not prompt:
        prompt = "create todo api dotnet app"

    result = run_agent(prompt, 20)

    print("\nFinal result:")
    print(result)


if __name__ == "__main__":
    main()