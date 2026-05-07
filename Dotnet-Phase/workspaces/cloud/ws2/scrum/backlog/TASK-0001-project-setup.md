# TASK-0001 Project Setup

## Goal

Create the initial .NET 8 Web API source project for TodoApp.

## Scope

Set up the backend API project only. Do not implement users, groups, roles, todos, JWT, or tests yet.

## Technical Tasks

- Create source/TodoApp.Api project folder.
- Create TodoApp.Api.csproj targeting net8.0.
- Add package references:
  - Microsoft.EntityFrameworkCore
  - Npgsql.EntityFrameworkCore.PostgreSQL
  - Microsoft.EntityFrameworkCore.Design
  - Swashbuckle.AspNetCore
- Create Program.cs using minimal hosting model.
- Create appsettings.json with PostgreSQL connection string placeholder.
- Create folders:
  - Controllers
  - Services
  - Repositories
  - Models
  - DTOs
  - Data
- Create Data/AppDbContext.cs.
- Create Controllers/HealthController.cs.
- Register AppDbContext in Program.cs.
- Enable controllers and Swagger.
- Run dotnet build for source/TodoApp.Api/TodoApp.Api.csproj.
- Update memory/project_map.json.
- Update memory/feature_history.md.

## Acceptance Criteria

- source/TodoApp.Api/TodoApp.Api.csproj exists.
- Program.cs exists and configures controllers, Swagger, and EF Core.
- appsettings.json exists.
- Data/AppDbContext.cs exists.
- Controllers/HealthController.cs exists.
- GET /api/health endpoint exists.
- dotnet build succeeds or the build error is logged clearly.
- No user, group, role, todo, JWT, or test implementation is included.

## Dependencies

- Planning files must already exist.

## Status

Ready
