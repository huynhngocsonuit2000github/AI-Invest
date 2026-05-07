# AI Scrum Team Agent — Technical Roadmap and Implementation Plan

## 1. Goal

Build an AI Scrum Team Agent system that can take a user request such as:

```text
Create a todo app with users, groups, and roles.
```

Then automatically convert it into:

```text
Requirement analysis
→ user stories
→ task breakdown
→ code implementation
→ tests
→ review
→ memory update
→ next task
```

The final system should support both:

```text
1. Building a new project from scratch
2. Continuing work on an existing project
```

The correct architecture is not one uncontrolled AI agent running for many days.

The correct architecture is:

```text
Deterministic orchestrator
+ role-based agents
+ local tool executor
+ persistent task state
+ safe workspace
+ build/test/review gates
+ resumable long-running workflow
```

---

## 2. Core Concept

The system should work like a local AI software team.

```text
User prompt
  ↓
BA Agent analyzes requirement
  ↓
Scrum Master Agent creates tasks
  ↓
Developer Agent implements code
  ↓
Tester Agent creates/runs tests
  ↓
Reviewer Agent checks code quality
  ↓
Memory Agent updates project knowledge
  ↓
Repeat until sprint/project is done
```

The important separation is:

```text
Cloud LLM = reasoning brain
Local tools = hands
Workspace = source code
Markdown/JSON files = Jira + Confluence + memory
Database = durable execution state
Orchestrator = deterministic controller
```

---

## 3. Recommended Technical Stack

## 3.1 Best Stack for Your Background

Because you work with full-stack .NET backend and DevOps, the recommended stack is:

```text
Backend / Orchestrator:
- C#
- .NET 8/9
- ASP.NET Core API
- .NET Worker Service

Agent Layer:
- OpenAI API
- Structured Outputs / tool calling
- Later: Microsoft Agent Framework or Semantic Kernel

Workflow / Background Jobs:
- MVP: Hangfire
- Production: Temporal

Database:
- PostgreSQL
- Optional MVP: SQLite

Frontend:
- React
- Next.js
- Tailwind CSS

Execution / Sandbox:
- Docker
- Git
- Git worktree
- Restricted command runner

Observability:
- Serilog
- OpenTelemetry
- Grafana / Prometheus later

Source Control:
- Git
- Pull-request-like task branches
- Git diff review per task
```

---

## 3.2 Purpose of Each Technology

| Technology                | Purpose                                                    |
| ------------------------- | ---------------------------------------------------------- |
| C# / .NET                 | Main backend, orchestration, API, workers                  |
| ASP.NET Core API          | Expose project/task/agent APIs                             |
| .NET Worker Service       | Run agent jobs in the background                           |
| OpenAI API                | LLM reasoning, task planning, code generation decisions    |
| Structured Outputs        | Force model response into reliable JSON schema             |
| Microsoft Agent Framework | Production-grade .NET/Python agent framework               |
| Semantic Kernel           | Agent patterns, tool calling, plugins, memory integration  |
| Hangfire                  | Simple persistent background jobs for .NET MVP             |
| Temporal                  | Durable workflow engine for multi-day execution            |
| PostgreSQL                | Store projects, sprints, tasks, agent runs, logs           |
| Docker                    | Isolated project execution and safe build/test environment |
| Git worktree              | Separate workspace per task/branch                         |
| React / Next.js           | Dashboard for monitoring tasks and approvals               |
| Serilog                   | Structured logging                                         |
| OpenTelemetry             | Distributed tracing for agent/tool execution               |
| Redis                     | Optional queue/cache/pub-sub                               |
| RabbitMQ/Kafka            | Optional advanced event-driven execution                   |

---

# 4. System Architecture

## 4.1 High-Level Architecture

```text
+----------------------+
| React / Next.js UI   |
+----------+-----------+
           |
           v
+----------------------+
| ASP.NET Core API     |
+----------+-----------+
           |
           v
+----------------------+
| Scrum Orchestrator   |
+----------+-----------+
           |
           v
+----------------------+
| Agent Runtime        |
| BA / SM / Dev / Test |
+----------+-----------+
           |
           v
+----------------------+
| Tool Executor        |
| read/write/run/git   |
+----------+-----------+
           |
           v
+----------------------+
| Docker Workspace     |
| source + memory      |
+----------+-----------+
           |
           v
+----------------------+
| Build/Test/Git Diff  |
+----------------------+
```

---

## 4.2 Main Backend Modules

```text
ScrumAgent.Api/
  Controllers/
    ProjectsController.cs
    SprintsController.cs
    TasksController.cs
    AgentRunsController.cs
    ApprovalsController.cs

  Application/
    Orchestrator/
      ScrumOrchestrator.cs
      TaskStateMachine.cs
      SprintRunner.cs

    Agents/
      BaAgent.cs
      ScrumMasterAgent.cs
      DeveloperAgent.cs
      TesterAgent.cs
      ReviewerAgent.cs
      MemoryAgent.cs

    Tools/
      FileTools.cs
      GitTools.cs
      CommandTools.cs
      DotnetTools.cs
      MemoryTools.cs
      DockerTools.cs

    Services/
      LlmClient.cs
      WorkspaceService.cs
      ProjectInspectionService.cs
      TaskPlannerService.cs
      ApprovalService.cs

  Infrastructure/
    Database/
    Repositories/
    Logging/
    Queue/
    Docker/
```

---

# 5. Agent Responsibilities

## 5.1 BA Agent

Purpose:

```text
Analyze user request and convert it into business-readable requirements.
```

Input:

```text
- user prompt
- existing product docs
- business_rules.md
- feature_history.md
```

Output:

```text
- product requirement document
- user stories
- acceptance criteria
- edge cases
- assumptions
```

Files created/updated:

```text
docs/product/feature-spec.md
scrum/backlog/TASK-xxxx.md
```

Does it generate source code?

```text
No. It generates requirement and task documents.
```

---

## 5.2 Scrum Master Agent

Purpose:

```text
Break requirements into small executable tasks.
```

Input:

```text
- BA output
- backlog
- known issues
- project memory
```

Output:

```text
- ordered task list
- sprint plan
- task priority
- task dependencies
```

Files created/updated:

```text
scrum/backlog/*.md
scrum/sprint/*.md
logs/sprint-summary.md
```

Does it generate source code?

```text
No. It generates task and sprint files.
```

---

## 5.3 Developer Agent

Purpose:

```text
Create or update application source code.
```

Input:

```text
- task file
- acceptance criteria
- project_map.json
- coding_conventions.md
- relevant source files
```

Output:

```text
- source code changes
- build result
- implementation summary
```

Files created/updated:

```text
source/**/*.cs
source/**/*.json
source/**/*.csproj
source/**/*.sln
source/**/*.ts
source/**/*.tsx
source/**/*.sql
```

Does it generate source code?

```text
Yes. This is the main source-code generator.
```

---

## 5.4 Tester Agent

Purpose:

```text
Create or run tests based on acceptance criteria.
```

Input:

```text
- task file
- acceptance criteria
- changed files
- test_knowledge.md
```

Output:

```text
- test files
- test results
- bug report if failed
```

Files created/updated:

```text
tests/**/*.cs
tests/**/*.feature
tests/**/*.json
memory/test_knowledge.md
logs/test-results/*.md
```

Does it generate source code?

```text
Yes. It generates test code.
```

---

## 5.5 Reviewer Agent

Purpose:

```text
Review code diff before task is marked done.
```

Input:

```text
- git diff
- coding_conventions.md
- acceptance criteria
- build/test results
```

Output:

```text
- approved
- changes requested
- review comments
```

Files created/updated:

```text
logs/reviews/TASK-xxxx-review.md
memory/known_issues.md if needed
```

Does it generate source code?

```text
Usually no. It can request changes, but Developer Agent should apply them.
```

---

## 5.6 Memory Agent

Purpose:

```text
Update project knowledge after successful verification.
```

Input:

```text
- completed task
- changed files
- build/test/review result
```

Output:

```text
- updated project map
- updated feature history
- updated test knowledge
- updated known issues
```

Files created/updated:

```text
memory/project_map.json
memory/business_rules.md
memory/feature_history.md
memory/test_knowledge.md
memory/coding_conventions.md
memory/known_issues.md
```

Does it generate source code?

```text
No. It updates memory and documentation.
```

---

# 6. Project Workspace Structure

Recommended local workspace:

```text
workspace/
  source/
    YourApplication/

  scrum/
    backlog/
    sprint/
    done/
    templates/

  docs/
    product/
    architecture/
    api/
    decisions/

  memory/
    project_map.json
    business_rules.md
    feature_history.md
    test_knowledge.md
    coding_conventions.md
    known_issues.md
    agent_notes.md

  logs/
    task_runs/
    build/
    tests/
    reviews/
    tool_calls/

  worktrees/
    TASK-0001/
    TASK-0002/
```

---

# 7. Task State Machine

Each task should move through strict states.

```text
BACKLOG
  ↓
ANALYZED
  ↓
READY_FOR_DEV
  ↓
DEV_IN_PROGRESS
  ↓
BUILD_RUNNING
  ↓
BUILD_PASSED / BUILD_FAILED
  ↓
TEST_RUNNING
  ↓
TEST_PASSED / TEST_FAILED
  ↓
REVIEW_RUNNING
  ↓
REVIEW_PASSED / REVIEW_FAILED
  ↓
DONE
```

Failure flow:

```text
BUILD_FAILED
  → Developer Agent fixes
  → build again

TEST_FAILED
  → Developer Agent or Tester Agent fixes
  → test again

REVIEW_FAILED
  → Developer Agent fixes review comments
  → review again
```

Hard limits:

```text
max_iterations_per_task = 20
max_retries_per_task = 3
max_files_changed_per_task = 20
max_command_runtime_minutes = 5
human_approval_required_for_dangerous_commands = true
```

---

# 8. Tool Layer

The LLM should not directly control your computer.

It should only request safe tool actions.

## 8.1 MVP Tools

```text
list_files
read_file
write_file
replace_in_file
run_command
```

## 8.2 Recommended Production Tools

```text
inspect_project
create_backup
restore_backup
git_status
git_diff
git_worktree_create
git_worktree_remove
run_dotnet_build
run_dotnet_test
run_npm_build
run_npm_test
docker_run
update_memory
move_task_status
write_task_log
```

## 8.3 Dangerous Commands to Block or Require Approval

```text
rm -rf
del /s
format
diskpart
shutdown
curl | bash
sudo
chmod -R 777
commands outside workspace
network commands unless explicitly allowed
secret-reading commands
```

---

# 9. LLM JSON Action Protocol

Use structured JSON from the model.

Example response from the model:

```json
{
  "agent": "developer",
  "taskId": "TASK-0001",
  "thoughtSummary": "Need to inspect project and create Todo API structure.",
  "actions": [
    {
      "tool": "list_files",
      "args": {
        "path": "workspace/source"
      }
    },
    {
      "tool": "run_dotnet_build",
      "args": {
        "projectPath": "workspace/source/TodoApi/TodoApi.csproj"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}
```

Important rules:

```text
- Model returns JSON only
- Orchestrator validates JSON schema
- Tool executor validates paths and commands
- Tool result is sent back to model
- Repeat until done=true or retry limit is reached
```

---

# 10. Database Design

Recommended tables:

## 10.1 projects

```text
id
name
description
workspace_path
tech_stack
status
created_at
updated_at
```

## 10.2 sprints

```text
id
project_id
name
goal
status
start_date
end_date
created_at
updated_at
```

## 10.3 tasks

```text
id
project_id
sprint_id
task_key
title
description
acceptance_criteria
status
priority
assigned_agent
retry_count
created_at
updated_at
```

## 10.4 agent_runs

```text
id
task_id
agent_name
status
input_summary
output_summary
started_at
ended_at
error_message
```

## 10.5 tool_calls

```text
id
agent_run_id
tool_name
input_json
output_json
status
started_at
ended_at
```

## 10.6 approvals

```text
id
task_id
approval_type
requested_by_agent
reason
status
approved_by
created_at
updated_at
```

## 10.7 memory_entries

```text
id
project_id
memory_type
file_path
summary
updated_by_task
created_at
updated_at
```

---

# 11. Phase Roadmap

## Phase 1 — Local Console Agent

Goal:

```text
Prove that the agent can create/update code locally.
```

Technical stack:

```text
- .NET Console App
- OpenAI API
- local file tools
- local command runner
- markdown memory files
```

Output:

```text
ScrumAgent.Console/
  Program.cs
  AgentLoop.cs
  OpenAiClient.cs
  Tools/
    ReadFileTool.cs
    WriteFileTool.cs
    ReplaceInFileTool.cs
    RunCommandTool.cs
    ListFilesTool.cs
  workspace/
    source/
    scrum/
    memory/
    logs/
```

Can this phase generate code?

```text
Yes.
```

What it can do:

```text
- create a new .NET project
- edit source files
- run dotnet build
- run dotnet test
- create simple logs
```

Definition of done:

```text
User prompt: "Create Todo API"
Agent creates project
Agent creates source files
Agent runs dotnet build
Build passes
Final summary is written
```

---

## Phase 2 — API, Database, and Background Jobs

Goal:

```text
Make the agent persistent and controllable.
```

Technical stack:

```text
- ASP.NET Core API
- PostgreSQL
- Entity Framework Core
- Hangfire
- .NET Worker Service
```

Output:

```text
ScrumAgent.Api/
  Controllers/
  Services/
  Jobs/
  Database/
  Migrations/
```

Can this phase generate code?

```text
Yes.
```

But the main purpose is:

```text
Persist task state, job state, logs, retries, and project configuration.
```

Definition of done:

```text
- Create project through API
- Create task through API
- Run task as background job
- Save task status in database
- Resume after app restart
```

---

## Phase 3 — Dashboard and Task Control

Goal:

```text
Let user monitor and control the AI Scrum Team.
```

Technical stack:

```text
- React
- Next.js
- Tailwind CSS
- SignalR or WebSocket
```

Output:

```text
scrum-agent-dashboard/
  pages/
    projects
    backlog
    sprint
    task-detail
    agent-runs
  components/
    TaskBoard.tsx
    LogViewer.tsx
    ApprovalPanel.tsx
    DiffViewer.tsx
```

Can this phase generate code?

```text
Not mainly.
```

Main purpose:

```text
- show backlog
- show sprint board
- show live logs
- approve risky actions
- retry failed tasks
- inspect git diff
```

Definition of done:

```text
- Dashboard lists projects
- Dashboard shows task status
- User can start/stop/retry a task
- User can approve or reject risky actions
```

---

## Phase 4 — Docker Sandbox and Git Worktree

Goal:

```text
Make code generation safer.
```

Technical stack:

```text
- Docker
- Git
- Git worktree
- restricted command runner
```

Output:

```text
workspace/
  repos/main-project/
  worktrees/TASK-0001/
  worktrees/TASK-0002/

docker/
  Dockerfile.agent
  docker-compose.yml
```

Can this phase generate code?

```text
Yes.
```

Main purpose:

```text
- isolate generated code changes
- run build/test in container
- prevent writes outside workspace
- create clean diff per task
- rollback easily
```

Definition of done:

```text
- Each task runs in isolated worktree
- Docker runs build/test
- Git diff is captured
- Failed task can be discarded
- Passed task can be merged
```

---

## Phase 5 — Multi-Agent Framework

Goal:

```text
Replace one generic agent with role-based agents.
```

Technical stack:

```text
- Microsoft Agent Framework
  or
- Semantic Kernel
```

Output:

```text
Agents/
  BaAgent.cs
  ScrumMasterAgent.cs
  DeveloperAgent.cs
  TesterAgent.cs
  ReviewerAgent.cs
  MemoryAgent.cs
```

Can this phase generate code?

```text
Yes.
```

Main code-generating agents:

```text
Developer Agent -> product code
Tester Agent -> test code
Memory Agent -> memory/docs updates
```

Definition of done:

```text
- BA creates acceptance criteria
- Scrum Master creates tasks
- Developer implements code
- Tester runs tests
- Reviewer checks diff
- Memory updates only after success
```

---

## Phase 6 — Durable Multi-Day Workflow

Goal:

```text
Allow the system to run safely for many hours or days.
```

Technical stack:

```text
- Temporal
- PostgreSQL
- Worker services
- event history
- retry policies
- human approval gates
```

Output:

```text
Workflows/
  SprintWorkflow.cs
  TaskWorkflow.cs
  CodeGenerationWorkflow.cs
  ReviewWorkflow.cs

Activities/
  RunBaAgentActivity.cs
  RunDeveloperAgentActivity.cs
  RunTesterAgentActivity.cs
  RunReviewerAgentActivity.cs
```

Can this phase generate code?

```text
Yes.
```

Main purpose:

```text
- long-running execution
- checkpoint every step
- resume after crash
- retry failed activities
- pause for approval
- continue sprint until complete
```

Definition of done:

```text
- Sprint can contain many tasks
- Workflow survives service restart
- Failed task retries safely
- Human approval pauses/resumes workflow
- Final sprint report is generated
```

---

# 12. Which Phase Creates Code?

| Phase   | Creates/updates source code? | Explanation                                               |
| ------- | ---------------------------: | --------------------------------------------------------- |
| Phase 1 |                          Yes | First working local code generator                        |
| Phase 2 |                          Yes | Same generation, but persistent through API/jobs/database |
| Phase 3 |                   Not mainly | Dashboard controls generation                             |
| Phase 4 |                          Yes | Safer code generation using Docker/worktree               |
| Phase 5 |                          Yes | Smarter role-based code generation                        |
| Phase 6 |                          Yes | Durable multi-day code generation                         |

Direct answer:

```text
Code generation starts in Phase 1.
```

What improves later:

```text
Phase 1: can generate code
Phase 2: can save state
Phase 3: can control from UI
Phase 4: can generate safely
Phase 5: can generate with role agents
Phase 6: can generate for days with resume/retry
```

---

# 13. Recommended Build Order

Recommended practical order:

```text
1. Phase 1 — Local console agent
2. Phase 2 — API + database + Hangfire
3. Phase 4 — Docker sandbox + Git worktree
4. Phase 3 — Dashboard
5. Phase 5 — Multi-agent framework
6. Phase 6 — Temporal durable workflow
```

Reason:

```text
First prove code generation.
Then persist it.
Then make it safe.
Then make it visible.
Then make it smarter.
Then make it long-running.
```

---

# 14. MVP Implementation Plan

## Step 1 — Create Console App

```bash
dotnet new console -n ScrumAgent.Console
```

Add:

```text
AgentLoop.cs
OpenAiClient.cs
ToolRegistry.cs
FileTools.cs
CommandTools.cs
```

---

## Step 2 — Define Tool Schema

Example tools:

```json
[
  {
    "name": "read_file",
    "description": "Read a file inside workspace",
    "parameters": {
      "path": "string"
    }
  },
  {
    "name": "write_file",
    "description": "Write file inside workspace",
    "parameters": {
      "path": "string",
      "content": "string"
    }
  },
  {
    "name": "run_command",
    "description": "Run approved command inside workspace",
    "parameters": {
      "command": "string",
      "workingDirectory": "string"
    }
  }
]
```

---

## Step 3 — Add Workspace Protection

Rules:

```text
- Normalize all paths
- Block absolute paths outside workspace
- Block dangerous commands
- Limit command timeout
- Limit output size
- Backup before write
```

---

## Step 4 — Add Agent Loop

Pseudo-flow:

```text
while not done:
  send prompt + tools + previous results to LLM
  parse structured JSON
  validate action schema
  execute tool actions
  save tool results
  send result back to LLM
```

---

## Step 5 — Add Build/Test Gate

For .NET project:

```bash
dotnet build
dotnet test
```

Rules:

```text
- If build fails, return error to Developer Agent
- If test fails, return error to Developer/Tester Agent
- If both pass, continue to review
```

---

## Step 6 — Add Git Diff Review

Commands:

```bash
git status
git diff
```

Reviewer checks:

```text
- does code match acceptance criteria?
- are files changed correctly?
- are conventions followed?
- is there risky code?
- are tests included?
```

---

## Step 7 — Add Memory Update

Only update memory after:

```text
build passed
test passed
review passed
```

Update:

```text
memory/project_map.json
memory/feature_history.md
memory/test_knowledge.md
memory/known_issues.md
```

---

# 15. Example Full Workflow

User prompt:

```text
Create todo app with users, groups, and roles.
```

Expected generated tasks:

```text
TASK-0001 Initialize .NET Web API project
TASK-0002 Add database and EF Core setup
TASK-0003 Add User entity and CRUD API
TASK-0004 Add Group entity and CRUD API
TASK-0005 Add Role entity and CRUD API
TASK-0006 Add user-group relationship
TASK-0007 Add user-role relationship
TASK-0008 Add validation and DTOs
TASK-0009 Add authentication
TASK-0010 Add unit/integration tests
TASK-0011 Generate API documentation
```

Expected execution:

```text
BA Agent:
  writes product spec and acceptance criteria

Scrum Master Agent:
  creates task files

Developer Agent:
  implements TASK-0001

Tester Agent:
  runs tests

Reviewer Agent:
  reviews git diff

Memory Agent:
  updates memory after success

Orchestrator:
  moves TASK-0001 to done and starts TASK-0002
```

---

# 16. Safety and Reliability Rules

Mandatory rules:

```text
- Use dedicated workspace only
- Never write outside workspace
- Never run unrestricted shell commands
- Backup before file overwrite
- Prefer replace_in_file over full overwrite
- Run build after source code changes
- Run tests after implementation
- Review git diff before done
- Update memory only after success
- Save logs for every tool call
- Stop after retry limit
- Require approval for risky actions
```

---

# 17. Recommended Final Architecture

```text
Frontend:
  React + Next.js + Tailwind

Backend:
  ASP.NET Core API

Workers:
  .NET Worker Service

Agent runtime:
  OpenAI API
  Structured Outputs
  Microsoft Agent Framework or Semantic Kernel later

Workflow:
  Hangfire for MVP
  Temporal for production

Database:
  PostgreSQL

Workspace:
  Docker sandbox
  Git worktree per task

Memory:
  Markdown + JSON files
  PostgreSQL metadata

Quality gates:
  dotnet build
  dotnet test
  git diff review
  human approval when needed
```

---

# 18. Final Recommendation

Build the system in this order:

```text
MVP:
  .NET Console Agent
  OpenAI API
  read/write/run tools
  build/test gate

Next:
  ASP.NET Core API
  PostgreSQL
  Hangfire
  task state machine

Then:
  Docker sandbox
  Git worktree
  dashboard

Finally:
  multi-agent framework
  Temporal durable workflow
```

The most important design rule:

```text
The AI should suggest actions.
The orchestrator should control actions.
The tool executor should enforce safety.
The build/test/review gates should decide success.
Memory should update only after verified success.
```

---

# 19. Official Reference Links

- Microsoft Agent Framework: https://learn.microsoft.com/en-us/agent-framework/overview/
- Semantic Kernel Agent Framework: https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/
- OpenAI Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- Temporal Workflows: https://docs.temporal.io/workflows
- Hangfire Documentation: https://docs.hangfire.io/
- Docker Documentation: https://docs.docker.com/
- Git Worktree Documentation: https://git-scm.com/docs/git-worktree
- PostgreSQL Documentation: https://www.postgresql.org/docs/
