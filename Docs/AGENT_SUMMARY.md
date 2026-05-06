# Autonomous Coding Agent Summary

## What we built

We built a small local coding agent in Python.

The agent accepts a simple natural-language prompt, such as:

```text
create todo api dotnet app
```

Then it asks a cloud LLM what actions to take. The LLM does not directly modify files. Instead, it returns JSON tool calls, and the Python script executes those tools locally.

## Final architecture

```text
User prompt
   ↓
Python agent loop
   ↓
Cloud LLM through LiteLLM/OpenRouter
   ↓
LLM returns JSON actions
   ↓
Python runs local tools
   ↓
Tool results are sent back to the LLM
   ↓
Repeat until done
```

## Tech stack

```text
Python
  Main runtime for the agent.

LiteLLM
  Library used to call cloud LLM providers with one common API.

OpenRouter
  Cloud LLM provider used by the current setup.

python-dotenv
  Loads settings from the .env file.

Local tool functions
  list_files, read_file, write_file, run_command.

.NET SDK
  Used when the agent creates or builds .NET applications.
```

## Why we stopped using OpenHands

OpenHands caused too many issues in the native Windows setup:

```text
- Windows path error because some OpenHands skill files contain ':'
- Skill-loading errors
- Headless CLI instability
- False success summaries
- Unwanted project structures
- Hard-to-control file generation
```

The new approach is simpler and more controlled.

Instead of:

```text
CrewAI → OpenHands → hope files are generated correctly
```

We now use:

```text
Python agent → LLM JSON tool calls → local controlled tools
```

## How the agent works

### 1. User enters a prompt

Example:

```text
create todo api dotnet app
```

### 2. Python sends the prompt to the LLM

The request includes:

```text
- The system prompt
- The user request
- The workspace path
```

The system prompt tells the LLM:

```text
Return JSON only.
Use tools to inspect, write, and verify code.
Do not only explain code.
```

### 3. LLM responds with tool actions

Example:

```json
{
  "thought": "I need to create a new .NET Web API project.",
  "actions": [
    {
      "tool": "run_command",
      "args": {
        "command": "dotnet new webapi -n TodoApi"
      }
    }
  ],
  "done": false,
  "final_answer": ""
}
```

### 4. Python executes the tools

The Python script maps the requested tool name to a real function:

```text
list_files  -> list files in PROJECT_DIR
read_file   -> read a text file
write_file  -> create or overwrite a file
run_command -> run terminal command
```

### 5. Tool results go back to the LLM

Example tool result:

```text
STDOUT:
The template "ASP.NET Core Web API" was created successfully.

Exit code: 0
```

The LLM uses this result to decide the next step.

### 6. The loop continues

The LLM may then create files:

```json
{
  "tool": "write_file",
  "args": {
    "path": "TodoApi/Models/Todo.cs",
    "content": "..."
  }
}
```

Then it may run:

```json
{
  "tool": "run_command",
  "args": {
    "command": "dotnet build TodoApi/TodoApi.csproj"
  }
}
```

### 7. The agent finishes

When complete, the LLM returns:

```json
{
  "done": true,
  "final_answer": "Todo API created and build succeeded."
}
```

## Current files

### main.py

The main Python script.

Main responsibilities:

```text
- Load environment variables
- Call the LLM
- Parse JSON responses
- Run tools
- Trim conversation history
- Print final result
```

### .env

Configuration file.

Recommended shape:

```env
OPENROUTER_API_KEY=your_key_here
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_MODEL=openrouter/meta-llama/llama-3.3-70b-instruct
PROJECT_DIR=D:\me\AI Investigation\agent-scrum\workspace
```

## Why PROJECT_DIR matters

`PROJECT_DIR` is the workspace that the agent can modify.

Good:

```env
PROJECT_DIR=D:\me\AI Investigation\agent-scrum\workspace
```

Bad:

```env
PROJECT_DIR=D:\me\AI Investigation\agent-scrum
```

The root folder can contain:

```text
.venv
bin
obj
logs
old projects
large generated files
```

If the agent reads too much, the LLM request can exceed the model context limit.

## Important safety controls

### list_files ignores large folders

The script ignores:

```text
.git
.venv
bin
obj
node_modules
dist
build
```

This prevents huge file lists from being sent to the model.

### read_file blocks generated/binary files

The script refuses to read files such as:

```text
.dll
.exe
.pdb
.cache
.assets
.lock
```

### read_file truncates large files

Large files are limited to 12,000 characters.

### run_command truncates output

Command output is limited to the last 8,000 characters.

### trim_messages reduces conversation history

This prevents context-length errors by keeping only:

```text
- system prompt
- original user request
- latest messages
```

## How to run

From CMD:

```cmd
cd "D:\me\AI Investigation\agent-scrum"
.venv\Scripts\activate
chcp 65001
python main.py
```

Then enter a prompt:

```text
create todo api dotnet app
```

## Example prompts

```text
create todo api dotnet app
```

```text
add JWT authentication to the todo api
```

```text
add sqlite persistence to the todo api
```

```text
add unit tests for todo service
```

```text
fix build errors
```

```text
convert this api to clean architecture
```

## Mental model

```text
LLM = brain
Python script = agent runtime
Tools = hands
PROJECT_DIR = workspace
.NET SDK = compiler/build system
```

## Next improvements

Useful next steps:

```text
1. Add command allowlist for safer terminal execution.
2. Add git diff output after each task.
3. Add automatic backup before editing files.
4. Add a max file write size.
5. Add approval before dangerous commands.
6. Add support for multiple model providers.
7. Add log files for every agent run.
```

## Key lesson

The best design for this workflow is not a hardcoded Todo generator.

The better design is a generic local coding agent:

```text
Dynamic prompt
  ↓
LLM decides actions
  ↓
Python executes tools
  ↓
Build/test verifies result
```

This lets you reuse the same agent for many software engineering tasks.
