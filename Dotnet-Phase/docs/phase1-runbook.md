# Phase 1 Runbook

## Goal

Create a local console agent that can generate files and code inside a safe workspace.

## Phase 1 output

```text
ScrumAgent.Console
workspace/source
workspace/scrum
workspace/memory
workspace/logs
```

## Phase 1 responsibilities

```text
- accept user prompt
- call LLM
- parse JSON action response
- execute local tools
- log tool results
- repeat until done=true
```

## What can create code in Phase 1

The single MVP agent can create code through:

```text
write_file
replace_in_file
run_command
```

Examples:

```text
write_file -> create .cs files
replace_in_file -> update existing .cs files
run_command -> run dotnet new, dotnet build, dotnet test
```

## Recommended first milestone

Prompt:

```text
Create project planning files for a todo app with users, groups, and roles. Do not create source code yet. Create backlog tasks and memory files.
```

Expected output:

```text
workspace/scrum/backlog/TASK-0001.md
workspace/scrum/backlog/TASK-0002.md
workspace/scrum/backlog/TASK-0003.md
workspace/memory/business_rules.md
workspace/memory/coding_conventions.md
```

## Recommended second milestone

Prompt:

```text
Start TASK-0001 and create a minimal .NET Web API project under source/TodoApi. Run dotnet build after creating it.
```

Expected output:

```text
workspace/source/TodoApi
workspace/logs/task_runs
```

## Stop conditions

Stop the loop when:

```text
- task is complete
- build/test passed
- max iteration reached
- dangerous command requested
- command timeout reached
```
