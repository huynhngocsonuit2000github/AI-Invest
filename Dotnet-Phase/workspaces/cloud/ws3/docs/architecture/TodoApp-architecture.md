# TodoApp Architecture

## Tech Stack
*.NET 8
* PostgreSQL
* JWT
* xUnit

## Application Layers
* Presentation
* Application
* Domain
* Infrastructure

## Folder Structure
* source/TodoApp.sln
* source/src/TodoApp.Api
* source/src/TodoApp.Application
* source/src/TodoApp.Domain
* source/src/TodoApp.Infrastructure
* source/tests/TodoApp.Tests

## Database or Storage Plan
* PostgreSQL database for storing users, groups, roles, and todos

## Authentication and Authorization Plan
* JWT authentication
* Role-based access control

## Dependency Flow
* The presentation layer depends on the application layer
* The application layer depends on the domain layer
* The domain layer depends on the infrastructure layer

## Build/Test Strategy
* Use xUnit for unit testing
* Use dotnet build and dotnet test for building and testing the app