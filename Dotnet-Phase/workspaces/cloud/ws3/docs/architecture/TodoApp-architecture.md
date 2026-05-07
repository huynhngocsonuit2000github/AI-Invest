# TodoApp Architecture

## Tech Stack
* .NET 8
* PostgreSQL
* JWT
* xUnit
## Application Layers
* API
* Service
* Repository
* Data
## Folder Structure
* source/TodoApp.Api
* source/TodoApp.Api/Controllers
* source/TodoApp.Api/Services
* source/TodoApp.Api/Repositories
* source/TodoApp.Api/Models
* source/TodoApp.Api/Dtos
* source/TodoApp.Api/Data
* source/TodoApp.Api/Auth
## Database or Storage Plan
* PostgreSQL database
## Authentication and Authorization Plan
* JWT authentication
## Dependency Flow
* API -> Service -> Repository -> Data
## Build/Test Strategy
* xUnit tests