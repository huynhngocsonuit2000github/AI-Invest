# System Overview

## Architecture Style
Use a layered web application architecture for the initial implementation:
- Presentation/API layer for HTTP endpoints.
- Application/service layer for use cases and permission checks.
- Domain/model layer for users, groups, roles, memberships, and tasks.
- Data access layer for persistence.

## Core Domains
- Users: account identity and profile information.
- Groups: collaboration boundaries for shared tasks.
- Roles: permissions assigned globally or within a group.
- Tasks: work items owned by users or groups.

## Permission Model
- Admin: manages users, groups, roles, and all tasks.
- Group Leader: manages members and tasks within assigned groups.
- Member: manages own tasks and permitted group tasks.

## Data Relationships
- A user can belong to many groups.
- A group can contain many users.
- A user can have one or more roles.
- A task can be assigned to a user and optionally scoped to a group.
