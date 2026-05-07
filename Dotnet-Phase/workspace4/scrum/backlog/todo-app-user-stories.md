# Todo App Backlog

## Epic: User Management
- As a visitor, I want to register so that I can use the Todo App.
- As a user, I want to log in and log out so that my tasks remain private.
- As an admin, I want to manage user accounts so that the system stays organized.

## Epic: Group Management
- As an admin, I want to create groups so that users can collaborate around shared task lists.
- As a group leader, I want to add and remove group members so that membership stays current.
- As a member, I want to view groups I belong to so that I know where my tasks are organized.

## Epic: Role Management
- As an admin, I want to assign roles so that users receive the correct permissions.
- As a group leader, I want limited management permissions within my group so that I can manage work without full system access.
- As a member, I want permissions to restrict actions I cannot perform so that group data stays protected.

## Epic: Task Management
- As a user, I want to create tasks so that I can track work.
- As a user, I want to update task status so that progress is visible.
- As a group leader, I want to assign tasks to group members so that responsibilities are clear.
- As a user, I want due dates and priorities so that I can plan my work.

## Acceptance Criteria Themes
- Role checks are enforced before restricted actions.
- Group-scoped tasks are visible only to authorized group members.
- Task CRUD supports title, description, status, due date, priority, assignee, and group.
- Audit-relevant operations identify the acting user.
