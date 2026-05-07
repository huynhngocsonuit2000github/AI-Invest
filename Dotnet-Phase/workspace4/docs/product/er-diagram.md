# ERD (Entity Relationship Diagram)
## Entities
- User
- Group
- Role
- Task
## Attributes
- User (ID, Name, Email)
- Group (ID, Name)
- Role (ID, Name)
- Task (ID, Title, Description, Due Date)
## Relationships
- One-to-Many (User to Tasks)
- Many-to-Many (User to Groups, Role to Users)