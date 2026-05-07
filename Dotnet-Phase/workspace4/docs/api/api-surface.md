# API Surface Plan

## Users
- `POST /users/register`
- `POST /auth/login`
- `GET /users/{id}`
- `PUT /users/{id}`
- `DELETE /users/{id}`

## Groups
- `POST /groups`
- `GET /groups`
- `GET /groups/{id}`
- `PUT /groups/{id}`
- `DELETE /groups/{id}`
- `POST /groups/{id}/members`
- `DELETE /groups/{id}/members/{userId}`

## Roles
- `GET /roles`
- `POST /roles/assignments`
- `DELETE /roles/assignments/{id}`

## Tasks
- `POST /tasks`
- `GET /tasks`
- `GET /tasks/{id}`
- `PUT /tasks/{id}`
- `DELETE /tasks/{id}`
- `POST /tasks/{id}/assign`

## Cross-Cutting Requirements
- All endpoints except registration and login require authentication.
- Mutating endpoints require role checks.
- List endpoints should support filtering by group, assignee, status, due date, and priority.
