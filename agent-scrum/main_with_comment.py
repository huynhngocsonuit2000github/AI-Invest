"""
main.py

A small autonomous coding agent.

What this script does:
1. Reads a natural-language coding request from the user.
2. Sends the request to a cloud LLM through LiteLLM/OpenRouter.
3. Forces the LLM to return JSON tool calls.
4. Executes local tools such as:
   - list_files
   - read_file
   - write_file
   - run_command
5. Sends tool results back to the LLM.
6. Repeats until the LLM says the task is complete.

Important:
- The LLM does not directly access your files.
- The LLM only asks to use tools.
- This Python script is the layer that actually reads files, writes files,
  and runs terminal commands.
"""

import json
import os
import subprocess
from pathlib import Path
from typing import Any

from dotenv import load_dotenv
from litellm import completion


# Load environment variables from .env.
# Example:
# OPENROUTER_API_KEY=your_key
# LLM_BASE_URL=https://openrouter.ai/api/v1
# LLM_MODEL=openrouter/meta-llama/llama-3.3-70b-instruct
# PROJECT_DIR=D:\me\AI Investigation\agent-scrum\workspace
load_dotenv()


# PROJECT_DIR is the only folder that this agent should work inside.
# Keep this folder small and clean.
#
# Good:
#   D:\me\AI Investigation\agent-scrum\workspace
#
# Bad:
#   D:\me\AI Investigation\agent-scrum
#
# Why bad?
# The root folder may contain .venv, bin, obj, old generated projects,
# and large files. Sending those to the model can exceed context limits.
PROJECT_DIR = Path(os.getenv("PROJECT_DIR", "workspace"))
PROJECT_DIR.mkdir(parents=True, exist_ok=True)


def get_llm_response(messages: list[dict[str, str]]) -> str:
    """
    Send the current conversation to the cloud LLM and return the text response.

    The LLM is expected to return JSON only because SYSTEM_PROMPT tells it to.

    messages shape:
    [
        {"role": "system", "content": "..."},
        {"role": "user", "content": "..."}
    ]
    """

    response = completion(
        model=os.getenv("LLM_MODEL"),
        api_key=os.getenv("OPENROUTER_API_KEY"),
        api_base=os.getenv("LLM_BASE_URL"),
        messages=messages,
        temperature=0.2,
    )

    return response["choices"][0]["message"]["content"]


def list_files(path: str = ".") -> str:
    """
    Tool: list_files

    Lists files inside PROJECT_DIR.

    This is how the LLM inspects the workspace before deciding what to do.

    Safety / performance rules:
    - Ignores heavy folders like .venv, bin, obj, node_modules.
    - Limits output to 200 files.
    - Returns paths relative to PROJECT_DIR.
    """

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
        # Skip ignored folders anywhere in the path.
        if any(part in ignored_dirs for part in item.parts):
            continue

        # Only return files, not folders.
        if item.is_file():
            result.append(str(item.relative_to(PROJECT_DIR)))

        # Avoid sending huge file lists to the model.
        if len(result) >= 200:
            result.append("...file list truncated...")
            break

    return "\n".join(result) if result else "No files found."


def read_file(path: str) -> str:
    """
    Tool: read_file

    Reads a text file from PROJECT_DIR.

    The LLM uses this when it needs to inspect existing code before editing.

    Safety / performance rules:
    - Refuses binary/generated files.
    - Truncates large files to 12,000 characters.
    """

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
    """
    Tool: write_file

    Creates or overwrites a file inside PROJECT_DIR.

    The LLM provides:
    - path
    - content

    This function creates parent folders automatically.
    """

    target = PROJECT_DIR / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")

    return f"File written: {path}"


def run_command(command: str) -> str:
    """
    Tool: run_command

    Runs a terminal command inside PROJECT_DIR.

    Example commands:
    - dotnet new webapi -n TodoApi
    - dotnet build TodoApi/TodoApi.csproj
    - dotnet test
    - npm install
    - npm run build

    Safety / performance rules:
    - The command runs only inside PROJECT_DIR.
    - Output is truncated to the last 8,000 characters.
    - Timeout is 300 seconds.
    """

    result = subprocess.run(
        command,
        cwd=PROJECT_DIR,
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
    """
    Dispatches a tool call requested by the LLM.

    The LLM returns JSON like:
    {
      "tool": "write_file",
      "args": {
        "path": "TodoApi/Models/Todo.cs",
        "content": "..."
      }
    }

    This function maps the tool name to a real Python function.
    """

    if tool == "list_files":
        return list_files(args.get("path", "."))

    if tool == "read_file":
        return read_file(args["path"])

    if tool == "write_file":
        return write_file(args["path"], args["content"])

    if tool == "run_command":
        return run_command(args["command"])

    return f"Unknown tool: {tool}"


# This is the main instruction sent to the LLM.
#
# The most important rule:
# The model must return valid JSON only.
#
# Why?
# Python needs to parse the model output and execute tool calls.
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
"""


def parse_json_response(text: str) -> dict[str, Any]:
    """
    Parses the LLM response as JSON.

    Sometimes models wrap JSON in markdown fences like:
    ```json
    {...}
    ```

    This function removes those fences before parsing.
    """

    text = text.strip()

    if text.startswith("```json"):
        text = text.removeprefix("```json").removesuffix("```").strip()

    if text.startswith("```"):
        text = text.removeprefix("```").removesuffix("```").strip()

    return json.loads(text)


def trim_messages(messages: list[dict[str, str]]) -> list[dict[str, str]]:
    """
    Keeps conversation history small.

    Without this, every tool result is appended forever.
    Large file contents or command outputs can make the prompt exceed
    the model context limit.

    Strategy:
    - Keep system prompt.
    - Keep original user request.
    - Keep only the last 6 messages.
    """

    if len(messages) <= 8:
        return messages

    return [
        messages[0],  # system prompt
        messages[1],  # original user prompt
        *messages[-6:],
    ]


def run_agent(user_prompt: str, max_steps: int = 10) -> str:
    """
    Main agent loop.

    One loop step:
    1. Send messages to LLM.
    2. Parse JSON response.
    3. Execute requested tools.
    4. Send tool results back to LLM.
    5. Repeat until done=true or max_steps is reached.
    """

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

    for step in range(1, max_steps + 1):
        print(f"\n--- Agent step {step} ---")

        # Prevent context overflow before every LLM call.
        messages = trim_messages(messages)

        raw_response = get_llm_response(messages)
        print(raw_response)

        try:
            agent_response = parse_json_response(raw_response)
        except Exception as exc:
            # If the model returns invalid JSON, ask it to correct itself.
            messages.append(
                {
                    "role": "user",
                    "content": (
                        "Your previous response was invalid JSON. "
                        f"Error: {exc}. Return valid JSON only."
                    ),
                }
            )
            continue

        # The model sets done=true when it believes the task is complete.
        if agent_response.get("done"):
            return agent_response.get("final_answer", "Done.")

        actions = agent_response.get("actions", [])

        if not actions:
            # Force the model to use tools instead of explaining.
            messages.append(
                {
                    "role": "user",
                    "content": "No actions were provided. Use tools to perform the task.",
                }
            )
            continue

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

        # Save the model response and tool results into the conversation.
        # This lets the LLM decide the next action based on what happened.
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
    """
    CLI entry point.

    If the user presses Enter with no input, it uses a default demo prompt.
    """

    prompt = input("Enter coding request: ").strip()

    if not prompt:
        prompt = "create todo api dotnet app"

    result = run_agent(prompt)

    print("\nFinal result:")
    print(result)


if __name__ == "__main__":
    main()
