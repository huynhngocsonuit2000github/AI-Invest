# Todo App Architecture

## Tech Stack
- .NET 8
- PostgreSQL
- xUnit

## Application Layers
- Presentation Layer: API
- Business Layer: Services
- Data Access Layer: Repositories

## Folder Structure
- source/App.Api
- source/App.Services
- source/App.Repositories
- source/App.Tests

## Database or Storage Plan
- PostgreSQL database for storing user, group, role, and todo data.

## Authentication and Authorization Plan
- JWT authentication for securing API endpoints.

## Dependency Flow
- API -> Services -> Repositories -> Database

## Build/Test Strategy
- Build: dotnet build
- Test: dotnet test