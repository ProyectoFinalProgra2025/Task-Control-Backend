# API INVENTORY - Task Control Backend

**Generado:** 2025-11-28
**Framework:** ASP.NET Core 9
**Base URL:** `/api`

---

## ÍNDICE
1. [Autenticación](#autenticación)
2. [Empresas](#empresas)
3. [Usuarios](#usuarios)
4. [Tareas](#tareas)
5. [Chat en Tiempo Real](#chat-en-tiempo-real)
6. [SignalR Hub](#signalr-hub)

---

## AUTENTICACIÓN

### POST /api/auth/login
**Descripción:** Inicia sesión y obtiene tokens JWT
**Autorización:** Público
**Request Body:**
```json
{
  "email": "string",
  "password": "string"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Login exitoso",
  "data": {
    "tokens": {
      "accessToken": "string",
      "refreshToken": "string",
      "expiresIn": 1800
    },
    "usuario": {
      "id": "guid",
      "email": "string",
      "nombreCompleto": "string",
      "rol": "string",
      "empresaId": "guid"
    }
  }
}
```

### POST /api/auth/refresh
**Descripción:** Renueva el access token usando refresh token
**Autorización:** Público (requiere refresh token válido)
**Request Body:**
```json
{
  "refreshToken": "string"
}
```
**Response:**
```json
{
  "success": true,
  "data": {
    "accessToken": "string",
    "refreshToken": "string",
    "expiresIn": 1800
  }
}
```

### POST /api/auth/logout
**Descripción:** Revoca el refresh token actual
**Autorización:** Requiere autenticación
**Request Body:**
```json
{
  "refreshToken": "string"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Logout OK"
}
```

### POST /api/auth/register-adminempresa
**Descripción:** Registra una nueva empresa con su AdminEmpresa
**Autorización:** Público
**Request Body:**
```json
{
  "nombreEmpresa": "string",
  "direccionEmpresa": "string?",
  "telefonoEmpresa": "string?",
  "email": "string",
  "password": "string",
  "nombreCompleto": "string",
  "telefono": "string?"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Empresa registrada en Pending",
  "data": {
    "empresaId": "guid"
  }
}
```

### POST /api/auth/register-admingeneral
**Descripción:** Registra un nuevo AdminGeneral (superadmin)
**Autorización:** Público si es el primer admin, requiere AdminGeneral existente si ya hay uno
**Request Body:**
```json
{
  "email": "string",
  "password": "string",
  "nombreCompleto": "string",
  "telefono": "string?"
}
```
**Response:**
```json
{
  "success": true,
  "message": "AdminGeneral creado correctamente",
  "data": {
    "id": "guid"
  }
}
```

---

## EMPRESAS

### GET /api/empresas
**Descripción:** Lista todas las empresas (solo AdminGeneral)
**Autorización:** AdminGeneral
**Query Params:**
- `estado` (optional): "Pending" | "Approved" | "Rejected"

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "nombre": "string",
      "estado": "string",
      "createdAt": "datetime"
    }
  ]
}
```

### GET /api/empresas/{id}/estadisticas
**Descripción:** Obtiene estadísticas de una empresa
**Autorización:** AdminGeneral o AdminEmpresa (de la empresa)
**Response:**
```json
{
  "success": true,
  "data": {
    "empresaId": "guid",
    "nombreEmpresa": "string",
    "totalTrabajadores": 0,
    "trabajadoresActivos": 0,
    "totalTareas": 0,
    "tareasPendientes": 0,
    "tareasAsignadas": 0,
    "tareasAceptadas": 0,
    "tareasFinalizadas": 0,
    "tareasCanceladas": 0
  }
}
```

### GET /api/empresas/{id}/trabajadores-ids
**Descripción:** Obtiene IDs de todos los trabajadores de una empresa
**Autorización:** AdminGeneral o AdminEmpresa (de la empresa)
**Response:**
```json
{
  "success": true,
  "data": {
    "empresaId": "guid",
    "trabajadoresIds": ["guid", "guid", ...]
  }
}
```

### PUT /api/empresas/{id}/aprobar
**Descripción:** Aprueba una empresa registrada
**Autorización:** AdminGeneral
**Response:**
```json
{
  "success": true,
  "message": "Empresa aprobada exitosamente"
}
```

### PUT /api/empresas/{id}/rechazar
**Descripción:** Rechaza una empresa registrada
**Autorización:** AdminGeneral
**Response:**
```json
{
  "success": true,
  "message": "Empresa rechazada exitosamente"
}
```

### DELETE /api/empresas/{id}
**Descripción:** Elimina permanentemente una empresa y todos sus datos
**Autorización:** AdminGeneral
**Response:**
```json
{
  "success": true,
  "message": "Empresa y todos sus datos relacionados han sido eliminados definitivamente"
}
```

---

## USUARIOS

### GET /api/usuarios/me
**Descripción:** Obtiene el perfil completo del usuario autenticado
**Autorización:** Requiere autenticación
**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "email": "string",
    "nombreCompleto": "string",
    "telefono": "string",
    "rol": "string",
    "empresaId": "guid",
    "departamento": "string?",
    "nivelHabilidad": 0,
    "isActive": true,
    "capacidades": [
      {
        "capacidadId": "guid",
        "nombre": "string",
        "nivel": 1
      }
    ]
  }
}
```

### GET /api/usuarios
**Descripción:** Lista usuarios de una empresa
**Autorización:** AdminEmpresa, AdminGeneral, ManagerDepartamento
**Query Params:**
- `rol` (optional): "Usuario" | "ManagerDepartamento"

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "nombreCompleto": "string",
      "email": "string",
      "rol": "string",
      "departamento": "string?",
      "nivelHabilidad": 0,
      "isActive": true
    }
  ]
}
```

### GET /api/usuarios/{id}
**Descripción:** Obtiene un usuario específico
**Autorización:** AdminEmpresa, AdminGeneral, o el propio usuario
**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "email": "string",
    "nombreCompleto": "string",
    "telefono": "string",
    "rol": "string",
    "empresaId": "guid",
    "departamento": "string?",
    "nivelHabilidad": 0,
    "isActive": true,
    "capacidades": [...]
  }
}
```

### POST /api/usuarios
**Descripción:** Crea un nuevo usuario en la empresa
**Autorización:** AdminEmpresa
**Request Body:**
```json
{
  "email": "string",
  "password": "string",
  "nombreCompleto": "string",
  "telefono": "string?",
  "rol": "Usuario" | "ManagerDepartamento",
  "departamento": "Ventas" | "Desarrollo" | "Soporte" | "RRHH" | "Operaciones" | "Finanzas",
  "nivelHabilidad": 1
}
```
**Response:**
```json
{
  "success": true,
  "message": "Usuario creado",
  "data": {
    "id": "guid"
  }
}
```

### PUT /api/usuarios/{id}
**Descripción:** Actualiza los datos de un usuario
**Autorización:** AdminEmpresa
**Request Body:**
```json
{
  "nombreCompleto": "string",
  "telefono": "string?",
  "departamento": "string?",
  "nivelHabilidad": 0
}
```
**Response:**
```json
{
  "success": true,
  "message": "Usuario actualizado"
}
```

### DELETE /api/usuarios/{id}
**Descripción:** Desactiva un usuario (soft delete)
**Autorización:** AdminEmpresa
**Response:**
```json
{
  "success": true,
  "message": "Usuario desactivado"
}
```

### PUT /api/usuarios/{id}/capacidades
**Descripción:** Actualiza las capacidades de un usuario (como admin)
**Autorización:** AdminEmpresa
**Request Body:**
```json
{
  "capacidades": [
    {
      "nombre": "string",
      "nivel": 1
    }
  ]
}
```
**Response:**
```json
{
  "success": true,
  "message": "Capacidades del usuario actualizadas"
}
```

### PUT /api/usuarios/mis-capacidades
**Descripción:** Actualiza las propias capacidades del usuario
**Autorización:** Requiere autenticación
**Request Body:**
```json
{
  "capacidades": [
    {
      "nombre": "string",
      "nivel": 1
    }
  ]
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tus capacidades han sido actualizadas"
}
```

### DELETE /api/usuarios/mis-capacidades/{capacidadId}
**Descripción:** Elimina una capacidad del propio perfil
**Autorización:** Requiere autenticación
**Response:**
```json
{
  "success": true,
  "message": "Capacidad eliminada de tu perfil"
}
```

---

## TAREAS

### POST /api/tareas
**Descripción:** Crea una nueva tarea en estado Pendiente
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:**
```json
{
  "titulo": "string",
  "descripcion": "string",
  "prioridad": "Low" | "Medium" | "High",
  "dueDate": "datetime?",
  "departamento": "Ventas" | "Desarrollo" | "Soporte" | "RRHH" | "Operaciones" | "Finanzas",
  "capacidadesRequeridas": ["string", "string"]
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tarea creada en estado PENDIENTE",
  "data": {
    "id": "guid"
  }
}
```

### GET /api/tareas
**Descripción:** Lista tareas según el rol del usuario
**Autorización:** Requiere autenticación
**Query Params:**
- `estado` (optional): "Pendiente" | "Asignada" | "Aceptada" | "Finalizada" | "Cancelada"
- `prioridad` (optional): "Low" | "Medium" | "High"
- `departamento` (optional): Departamento enum
- `asignadoA` (optional): guid

**Notas:**
- Usuario: Solo ve sus tareas asignadas
- ManagerDepartamento: Solo ve sus tareas asignadas
- AdminEmpresa: Ve todas las tareas de la empresa

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "titulo": "string",
      "descripcion": "string",
      "estado": "string",
      "prioridad": "string",
      "dueDate": "datetime?",
      "departamento": "string?",
      "asignadoAUsuarioId": "guid?",
      "asignadoAUsuarioNombre": "string?",
      "createdByUsuarioId": "guid",
      "createdByUsuarioNombre": "string",
      "estaDelegada": false,
      "delegadoPorUsuarioId": "guid?",
      "delegadoPorUsuarioNombre": "string?",
      "delegadoAUsuarioId": "guid?",
      "delegadoAUsuarioNombre": "string?",
      "delegacionAceptada": null,
      "motivoRechazoJefe": "string?",
      "createdAt": "datetime"
    }
  ]
}
```

### GET /api/tareas/mis
**Descripción:** Lista las tareas asignadas al usuario autenticado
**Autorización:** Usuario, ManagerDepartamento
**Query Params:**
- `estado` (optional)
- `prioridad` (optional)
- `departamento` (optional)

**Response:** Mismo formato que GET /api/tareas

### GET /api/tareas/{id}
**Descripción:** Obtiene el detalle completo de una tarea
**Autorización:** Requiere autenticación (Usuario solo puede ver sus propias tareas)
**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "empresaId": "guid",
    "titulo": "string",
    "descripcion": "string",
    "estado": "string",
    "prioridad": "string",
    "dueDate": "datetime?",
    "departamento": "string?",
    "capacidadesRequeridas": ["string"],
    "asignadoAUsuarioId": "guid?",
    "asignadoAUsuarioNombre": "string?",
    "createdByUsuarioId": "guid",
    "createdByUsuarioNombre": "string",
    "evidenciaTexto": "string?",
    "evidenciaImagenUrl": "string?",
    "estaDelegada": false,
    "delegadoPorUsuarioId": "guid?",
    "delegadoPorUsuarioNombre": "string?",
    "delegadoAUsuarioId": "guid?",
    "delegadoAUsuarioNombre": "string?",
    "delegadaAt": "datetime?",
    "delegacionAceptada": null,
    "motivoRechazoJefe": "string?",
    "delegacionResueltaAt": "datetime?",
    "createdAt": "datetime",
    "updatedAt": "datetime?"
  }
}
```

### PUT /api/tareas/{id}/asignar-manual
**Descripción:** Asigna una tarea manualmente a un worker
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:**
```json
{
  "usuarioId": "guid?",
  "nombreUsuario": "string?",
  "ignorarValidacionesSkills": false
}
```
**Notas:**
- Debe enviar usuarioId O nombreUsuario
- Valida capacidades y departamento a menos que ignorarValidacionesSkills sea true
- ManagerDepartamento solo puede asignar a workers de su departamento

**Response:**
```json
{
  "success": true,
  "message": "Tarea asignada manualmente"
}
```

### PUT /api/tareas/{id}/asignar-automatico
**Descripción:** Asigna una tarea automáticamente según capacidades y carga
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:**
```json
{
  "forzarReasignacion": false
}
```
**Response:**
```json
{
  "success": true,
  "message": "Asignación automática ejecutada"
}
```

### PUT /api/tareas/{id}/aceptar
**Descripción:** El worker acepta una tarea asignada
**Autorización:** Usuario, ManagerDepartamento
**Response:**
```json
{
  "success": true,
  "message": "Tarea aceptada"
}
```

### PUT /api/tareas/{id}/finalizar
**Descripción:** Finaliza una tarea con evidencia
**Autorización:** Usuario, ManagerDepartamento
**Request Body:**
```json
{
  "evidenciaTexto": "string?",
  "evidenciaImagenUrl": "string?"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tarea finalizada con evidencia"
}
```

### PUT /api/tareas/{id}/cancelar
**Descripción:** Cancela una tarea
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:** `"motivo opcional como string"`
**Response:**
```json
{
  "success": true,
  "message": "Tarea cancelada"
}
```

### PUT /api/tareas/{id}/reasignar
**Descripción:** Reasigna una tarea (wrapper para asignación manual/automática)
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:**
```json
{
  "usuarioId": "guid?",
  "asignacionAutomatica": false
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tarea reasignada"
}
```

### PUT /api/tareas/{id}/delegar
**Descripción:** Delega una tarea a otro jefe de área
**Autorización:** AdminEmpresa, ManagerDepartamento
**Request Body:**
```json
{
  "jefeDestinoId": "guid",
  "motivo": "string?"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Tarea delegada exitosamente. Esperando respuesta del jefe destino."
}
```

### PUT /api/tareas/{id}/aceptar-delegacion
**Descripción:** Acepta una tarea delegada
**Autorización:** ManagerDepartamento
**Request Body:**
```json
{
  "comentario": "string?"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Delegación aceptada. Ahora puedes gestionar esta tarea."
}
```

### PUT /api/tareas/{id}/rechazar-delegacion
**Descripción:** Rechaza una tarea delegada (motivo obligatorio)
**Autorización:** ManagerDepartamento
**Request Body:**
```json
{
  "motivo": "string"
}
```
**Response:**
```json
{
  "success": true,
  "message": "Delegación rechazada. La tarea regresa al jefe de origen."
}
```

---

## CHAT EN TIEMPO REAL

### GET /api/users/search
**Descripción:** Busca usuarios para iniciar chat
**Autorización:** Requiere autenticación
**Query Params:**
- `q` (optional): Término de búsqueda
- `take` (default: 20, max: 50): Número de resultados

**Notas de filtrado por rol:**
- AdminGeneral: Solo ve AdminEmpresa
- AdminEmpresa/Usuario: Solo ve usuarios de su empresa
- ManagerDepartamento: Ve AdminEmpresa, otros managers y workers de su departamento

**Response:**
```json
[
  {
    "id": "guid",
    "nombreCompleto": "string",
    "email": "string"
  }
]
```

### GET /api/chats
**Descripción:** Lista todos los chats del usuario autenticado
**Autorización:** Requiere autenticación
**Response:**
```json
[
  {
    "id": "guid",
    "type": "OneToOne" | "Group",
    "name": "string?",
    "members": [
      {
        "userId": "guid",
        "nombreCompleto": "string",
        "email": "string"
      }
    ],
    "lastMessage": {
      "id": "guid",
      "body": "string",
      "createdAt": "datetime",
      "senderId": "guid"
    }
  }
]
```

### POST /api/chats/one-to-one
**Descripción:** Crea o recupera un chat 1:1
**Autorización:** Requiere autenticación
**Request Body:**
```json
{
  "userId": "guid"
}
```
**Validaciones:**
- AdminGeneral solo puede chatear con AdminEmpresa
- Usuarios de diferentes empresas no pueden chatear (excepto AdminGeneral)

**Response:**
```json
{
  "id": "guid"
}
```

### POST /api/chats/group
**Descripción:** Crea un chat grupal
**Autorización:** Requiere autenticación
**Request Body:**
```json
{
  "name": "string",
  "memberIds": ["guid", "guid"]
}
```
**Response:**
```json
{
  "id": "guid"
}
```

### POST /api/chats/{chatId}/members
**Descripción:** Agrega un miembro a un chat grupal
**Autorización:** Requiere ser miembro del chat
**Request Body:**
```json
{
  "userId": "guid"
}
```
**Response:**
```json
{
  "chatId": "guid",
  "userId": "guid"
}
```

### GET /api/chats/{chatId}/messages
**Descripción:** Obtiene mensajes de un chat
**Autorización:** Requiere ser miembro del chat
**Query Params:**
- `skip` (default: 0): Mensajes a saltar
- `take` (default: 50, max: 100): Mensajes a obtener

**Response:**
```json
[
  {
    "id": "guid",
    "body": "string",
    "createdAt": "datetime",
    "senderId": "guid"
  }
]
```

### POST /api/chats/{chatId}/messages
**Descripción:** Envía un mensaje a un chat
**Autorización:** Requiere ser miembro del chat
**Request Body:**
```json
{
  "text": "string"
}
```
**Response:**
```json
{
  "id": "guid",
  "body": "string",
  "createdAt": "datetime",
  "senderId": "guid",
  "chatId": "guid"
}
```
**Nota:** También emite evento SignalR `chat:message` al grupo del chat

---

## SIGNALR HUB

### Endpoint: /apphub
**Descripción:** Hub WebSocket para comunicación en tiempo real
**Autenticación:** Query string `?access_token={jwt}`

### Métodos del Cliente → Servidor
- **JoinChat(Guid chatId)**: Suscribirse a mensajes de un chat
- **LeaveChat(Guid chatId)**: Desuscribirse de un chat

### Eventos del Servidor → Cliente

#### chat:message
Emitido cuando se envía un mensaje en un chat
**Payload:**
```json
{
  "id": "guid",
  "body": "string",
  "createdAt": "datetime",
  "senderId": "guid",
  "chatId": "guid"
}
```

#### empresa:created
Emitido cuando se registra una nueva empresa
**Grupo:** `super_admin`
**Payload:**
```json
{
  "id": "guid",
  "nombre": "string",
  "estado": "string",
  "createdAt": "datetime"
}
```

#### empresa:approved
Emitido cuando se aprueba una empresa
**Grupo:** `super_admin`
**Payload:**
```json
{
  "id": "guid",
  "nombre": "string",
  "estado": "string",
  "updatedAt": "datetime"
}
```

#### empresa:rejected
Emitido cuando se rechaza una empresa
**Grupo:** `super_admin`
**Payload:**
```json
{
  "id": "guid",
  "nombre": "string",
  "estado": "string",
  "updatedAt": "datetime"
}
```

#### tarea:created
Emitido cuando se crea una nueva tarea
**Grupos:** `empresa_{empresaId}`, `super_admin`
**Payload:**
```json
{
  "id": "guid",
  "titulo": "string",
  "empresaId": "guid",
  "estado": "string",
  "prioridad": "string",
  "departamento": "string?",
  "createdAt": "datetime"
}
```

#### tarea:assigned
Emitido cuando se asigna una tarea
**Grupos:** `empresa_{empresaId}`, `super_admin`
**Payload:**
```json
{
  "id": "guid",
  "titulo": "string",
  "empresaId": "guid",
  "estado": "string",
  "asignadoAUsuarioId": "guid",
  "asignadoANombre": "string",
  "updatedAt": "datetime"
}
```

---

## ENUMERACIONES

### RolUsuario
- `AdminGeneral` = 1
- `AdminEmpresa` = 2
- `Usuario` = 3
- `ManagerDepartamento` = 4

### EstadoEmpresa
- `Pending`
- `Approved`
- `Rejected`

### EstadoTarea
- `Pendiente`
- `Asignada`
- `Aceptada`
- `Finalizada`
- `Cancelada`

### PrioridadTarea
- `Low`
- `Medium`
- `High`

### Departamento
- `Ventas`
- `Desarrollo`
- `Soporte`
- `RRHH`
- `Operaciones`
- `Finanzas`

### ChatType
- `OneToOne`
- `Group`

### ChatRole
- `Owner`
- `Admin`
- `Member`

---

## RESUMEN DE ENDPOINTS

**Total de endpoints REST:** 32
**Endpoints SignalR:** 1 hub con 2 métodos y 6 eventos

### Por Controlador:
- **AuthController:** 5 endpoints
- **EmpresasController:** 5 endpoints
- **UsuariosController:** 10 endpoints
- **TareasController:** 12 endpoints
- **Chat (Minimal API):** 6 endpoints
- **SignalR Hub:** 1 endpoint (/apphub)

### Por Método HTTP:
- **GET:** 10 endpoints
- **POST:** 10 endpoints
- **PUT:** 11 endpoints
- **DELETE:** 3 endpoints

---

## NOTAS TÉCNICAS

### Autenticación
- **Access Token:** JWT con expiración de 30 minutos
- **Refresh Token:** 7 días de validez, se rota en cada uso
- **Claims incluidos:** Sub (userId), Role, empresaId (cuando aplica)

### Autorización
- Usa `[Authorize]` y filtro personalizado `[AuthorizeRole]`
- Validación a nivel de servicio para permisos de empresa y departamento

### CORS
- Configurado para localhost y https://taskcontrol.work
- Permite credenciales (necesario para SignalR)

### SignalR
- Autenticación vía query string: `?access_token={jwt}`
- Grupos dinámicos: `empresa_{id}`, `super_admin`
- JSON protocol habilitado

### Soft Delete
- Modelos con `IsActive` usan query filters globales
- EF Core filtra automáticamente registros inactivos

### Validaciones
- ModelState validation en controllers
- Business rules en services
- Límite de tareas activas por usuario (configurable, default: 5)
