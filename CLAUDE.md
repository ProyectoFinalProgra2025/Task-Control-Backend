# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TaskControl Backend is an ASP.NET Core 9.0 task management API with real-time features using SignalR. It provides a multi-tenant platform for companies to manage departments, users, and tasks with role-based access control and delegation workflows.

## Development Commands

### Build and Run
```bash
dotnet build
dotnet run
```

### Database Migrations
```bash
# Create a new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to database
dotnet ef database update

# Revert to a specific migration
dotnet ef database update <MigrationName>

# Remove last migration (if not applied)
dotnet ef migrations remove
```

### Testing
```bash
# Run all tests (when test project exists)
dotnet test

# Run with detailed output
dotnet test --verbosity detailed
```

## Architecture

### Technology Stack
- **Framework**: ASP.NET Core 9.0
- **Database**: SQL Server with Entity Framework Core 9.0
- **Authentication**: JWT Bearer tokens with refresh token rotation
- **Real-time**: SignalR for WebSocket-based communication
- **API Documentation**: Swagger/OpenAPI

### Project Structure
- **Controllers/**: API endpoints (Auth, Empresas, Tareas, Usuarios)
- **Services/**: Business logic layer with interface-based services
- **Models/**: Domain entities (Empresa, Usuario, Tarea, Chat entities)
- **Models/Enums/**: Enumerations (RolUsuario, EstadoTarea, Departamento, etc.)
- **Dtos/**: Data Transfer Objects organized by domain
- **Data/**: EF Core DbContext and entity configurations
- **Data/Configurations/**: Fluent API entity configurations
- **Hubs/**: SignalR hubs for real-time communication
- **Helpers/**: Utility classes (PasswordHasher, etc.)
- **Migrations/**: EF Core database migrations

### Database Context
All entities are accessed through `AppDbContext`. Key DbSets:
- `Empresas`: Companies
- `Usuarios`: Users (with soft-delete query filter)
- `Tareas`: Tasks
- `Capacidades`: Skills/capabilities
- `RefreshTokens`: JWT refresh tokens
- `Chats`, `ChatMembers`, `Messages`: Real-time chat system

Soft deletes are implemented globally via `IsActive` query filters on Empresa and Usuario entities.

### Dependency Injection
Services are registered in `Program.cs`:
- **Scoped services**: `ITokenService`, `IAuthService`, `IEmpresaService`, `IUsuarioService`, `ITareaService`
- All services follow interface-based patterns in `Services/Interfaces/`

## User Roles and Permissions

The system has 4 roles with hierarchical permissions:

### AdminGeneral (Super Admin)
- Manages all companies across the platform
- Can only chat with AdminEmpresa users
- Approves/rejects company registrations
- Has unrestricted access to all endpoints

### AdminEmpresa (Company Admin)
- Owns and manages their company
- Creates tasks for any department within their company
- Manages all users in their company
- Can chat with anyone in their company and with AdminGeneral

### ManagerDepartamento (Department Manager)
- Manages tasks only for their assigned department
- Can only assign workers from their own department
- Can delegate tasks to other department managers
- Must accept/reject delegated tasks from other managers
- Can chat with AdminEmpresa, other managers, and workers in their department

### Usuario (Worker/Employee)
- Accepts and executes assigned tasks
- Reports on task completion with evidence
- Can only view and interact with their assigned tasks
- Can chat within their company

## Key Domain Concepts

### Task Delegation Workflow
Department managers can delegate tasks to other department managers:
1. Manager creates or receives a task
2. Manager delegates task to another department manager (`/api/tareas/{id}/delegar`)
3. Target manager must explicitly accept or reject with reason (`/aceptar-delegacion` or `/rechazar-delegacion`)
4. Once accepted, target manager has full control of the task
5. Task tracks delegation history: `DelegadoPorUsuarioId`, `DelegadoAUsuarioId`, `DelegacionAceptada`

### Task Assignment Strategies
- **Manual Assignment**: AdminEmpresa or ManagerDepartamento assigns specific workers
- **Automatic Assignment**: System matches workers based on skills (`Capacidades`) and availability
- Department managers can only assign workers from their own department

### Real-time Features (SignalR)
The `ChatAppHub` at `/apphub` provides real-time communication:
- **Chat Groups**: One-to-one and group chats
- **Enterprise Groups**: Company-wide broadcasts (`empresa_{empresaId}`)
- **Department Groups**: Department-specific notifications (`empresa_{empresaId}_dept_{departamento}`)
- **Super Admin Group**: Platform-wide admin notifications

Connection requires JWT token via query string: `?access_token={jwt}`

Hub methods:
- `JoinChat(Guid chatId)` / `LeaveChat(Guid chatId)`
- `JoinEmpresaGroup(Guid empresaId)` / `LeaveEmpresaGroup(Guid empresaId)`
- `JoinDepartmentGroup(Guid empresaId, string departamento)`
- `JoinSuperAdminGroup()` (AdminGeneral only)

SignalR events emitted:
- `chat:message`: New chat messages
- `tarea:created`: New task notifications
- Other custom events for real-time updates

### Chat System Rules
- **AdminGeneral**: Can only chat with AdminEmpresa users
- **AdminEmpresa/Usuario**: Can chat with anyone in their company
- **ManagerDepartamento**: Can chat with:
  - Their AdminEmpresa
  - Other ManagerDepartamento in same company
  - Workers in their own department

Chat types: `OneToOne` (1:1 direct messages) and `Group` (multi-user groups)

### Authentication Flow
1. Login: `POST /api/auth/login` returns `accessToken` + `refreshToken`
2. Access token expires in 30 minutes (configurable in `appsettings.json`)
3. Refresh: `POST /api/auth/refresh` rotates tokens (revokes old refresh token)
4. Refresh tokens expire in 7 days
5. JWT includes claims: `sub` (userId), `role`, `empresaId`

## Configuration

### appsettings.json
- **ConnectionStrings:DefaultConnection**: SQL Server connection string
- **JwtSettings**: Token signing key, issuer, audience, expiration times
- **AppSettings:MaxTareasActivasPorUsuario**: Max active tasks per worker (default: 5)

### CORS Policy
The `DevCors` policy allows:
- `http://localhost:*`, `https://localhost:*`
- Production domain: `https://taskcontrol.work`
- Credentials enabled for SignalR

## Important Patterns

### Middleware Order (Critical)
From `Program.cs` lines 116-126:
```
UseHttpsRedirection()
UseRouting()
UseCors("DevCors")    // CORS MUST come before Authentication
UseAuthentication()
UseAuthorization()
```

### Claims Helpers
User identity is extracted via `ClaimsHelpers.GetUserId(principal)` from JWT claims (`sub` or `NameIdentifier`).

### Error Handling
Controllers should return structured error responses using `Results.BadRequest()`, `Results.Unauthorized()`, `Results.Forbid()`, etc.

### Validation Patterns
- Role-based validation: Check `principal` claims match required role
- Tenant isolation: Verify `empresaId` from claim matches URL parameter
- Department isolation: ManagerDepartamento can only access their department's resources

## Common Development Tasks

### Adding a New Endpoint
1. Add DTOs in `Dtos/<Domain>/`
2. Add service method in `Services/<Service>.cs` and interface
3. Add controller action in `Controllers/<Controller>.cs` or minimal API in `Program.cs`
4. Add authorization attribute/requirement if needed
5. Document in `ENDPOINTS.md`

### Adding a New Entity
1. Create model in `Models/`
2. Add DbSet to `AppDbContext`
3. Create configuration in `Data/Configurations/` (optional)
4. Create and apply migration
5. Add service methods for CRUD operations

### Adding SignalR Events
1. Define event in `Hubs/ChatAppHub.cs` or create new hub
2. Emit events using `IHubContext<ChatAppHub>` in services
3. Subscribe to events in frontend using `connection.on("event:name", handler)`

## Database Schema Notes

### Key Relationships
- `Usuario.EmpresaId` → `Empresa.Id` (many-to-one)
- `Tarea.CreatedByUsuarioId` → `Usuario.Id` (creator)
- `Tarea.AsignadoAUsuarioId` → `Usuario.Id` (assignee, nullable)
- `Tarea.DelegadoPorUsuarioId` / `DelegadoAUsuarioId` → `Usuario.Id` (delegation chain)
- `Chat.Members` ← `ChatMember` → `Usuario` (many-to-many)
- `Message.ChatId` → `Chat.Id`, `Message.SenderId` → `Usuario.Id`

### Enums
- **RolUsuario**: AdminGeneral(1), AdminEmpresa(2), Usuario(3), ManagerDepartamento(4)
- **Departamento**: Ninguno(0), Finanzas(1), Mantenimiento(2), Produccion(3), Marketing(4), Logistica(5)
- **EstadoTarea**: Pendiente, Asignada, EnProgreso, Finalizada, Cancelada
- **PrioridadTarea**: Low, Medium, High

## Testing Patterns

When adding tests:
- Use in-memory database or test database for integration tests
- Mock `IHubContext<ChatAppHub>` for SignalR-dependent services
- Test role-based authorization scenarios
- Verify tenant isolation in multi-tenant operations

## API Documentation

See `ENDPOINTS.md` for comprehensive endpoint documentation including:
- Authentication and authorization details
- Request/response formats
- Role-based access rules
- Delegation and assignment workflows
- SignalR connection details
