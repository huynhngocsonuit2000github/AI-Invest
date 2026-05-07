# Scrum Agent Phase 1 Starter

This is the Phase 1 MVP for the AI Scrum Team Agent.

It is a local .NET console application that:

- sends a user prompt to an LLM
- receives JSON actions
- validates and executes local tools
- reads/writes files only inside `workspace/`
- runs safe commands inside `workspace/`
- logs every model response and tool result

## What Phase 1 proves

Phase 1 proves that the agent can create or update code locally.

It does not yet include:

- PostgreSQL
- Hangfire
- dashboard
- Docker sandbox
- real multi-agent orchestration
- Temporal workflow

Those are later phases.

## Folder structure

```text
src/ScrumAgent.Console/     .NET console agent
workspace/source/           generated or existing source code
workspace/scrum/            backlog, sprint, done task files
workspace/memory/           project memory files
workspace/logs/             model and tool logs
```

## Requirements

- .NET 8 SDK or later
- OpenAI API key
- Git, optional

## Environment variables

Linux/macOS:

```bash
export OPENAI_API_KEY="your_api_key"
export OPENAI_MODEL="your_available_model"
export SCRUM_AGENT_WORKSPACE="$(pwd)/workspace"
```

Windows PowerShell:

```powershell
$env:OPENAI_API_KEY="your_api_key"
$env:OPENAI_MODEL="your_available_model"
$env:SCRUM_AGENT_WORKSPACE="$PWD\workspace"
```

## Run

```bash
dotnet run --project src/ScrumAgent.Console -- "Create a .NET todo API with users, groups, and roles"
```

## Expected behavior

The model should return JSON like this:

```json
{
  "thoughtSummary": "I need to create task files and initialize the project.",
  "actions": [
    {
      "tool": "write_file",
      "args": {
        "path": "scrum/backlog/TASK-0001.md",
        "content": "# TASK-0001 Initialize project"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}
```

Then the local tool executor writes the file.

## Available tools

```text
list_files
read_file
write_file
replace_in_file
run_command
```

## Safety rules in this starter

- All paths are forced inside `workspace/`
- Absolute writes outside workspace are blocked
- Known dangerous command fragments are blocked
- Commands timeout after 5 minutes
- File overwrite creates `.bak` backup
- Tool output is truncated before sending back to the model

## First test prompt

```text
Create project planning files for a todo app with users, groups, and roles. Do not create source code yet. Create backlog tasks and memory files.
```

## Second test prompt

After the first prompt succeeds:

```text
Start TASK-0001 and create a minimal .NET Web API project under source/TodoApi. Run dotnet build after creating it.
```

## Notes

This starter uses the OpenAI Responses API endpoint through `HttpClient`. The response parser extracts normal text output and expects the model to return JSON only.
