# BACKEND REVISION - TaskControl API

## Documento de Revisión Completa del Backend

Este documento proporciona una comprensión completa del backend de TaskControl para iniciar el desarrollo del frontend Flutter correctamente desde el primer día.

---

## 🎯 PROPÓSITO DEL SISTEMA

**TaskControl** es una plataforma empresarial de gestión de tareas multi-tenant (multi-empresa) que permite a las organizaciones:

1. **Gestionar múltiples empresas** desde una plataforma centralizada
2. **Organizar usuarios por roles jerárquicos** con permisos específicos
3. **Asignar tareas inteligentemente** basándose en habilidades y disponibilidad
4. **Delegar tareas entre departamentos** con flujos de aprobación
5. **Comunicarse en tiempo real** mediante WebSockets (SignalR)
6. **Rastrear el progreso de tareas** con evidencia de finalización

---

## 🏢 ARQUITECTURA MULTI-TENANT

### Modelo de Multi-Tenancy

```
PLATAFORMA TASKCONTROL
├── AdminGeneral (Super Admin)
│   ├── Gestiona TODAS las empresas
│   └── Aprueba/rechaza registros de empresas
│
├── EMPRESA A (Approved)
│   ├── AdminEmpresa
│   ├── Departamento Producción
│   │   ├── ManagerDepartamento
│   │   ├── Usuario (Worker) 1
│   │   └── Usuario (Worker) 2
│   ├── Departamento Finanzas
│   │   ├── ManagerDepartamento
│   │   └── Usuario (Worker) 3
│   └── Tareas, Capacidades, Chats (aislados)
│
├── EMPRESA B (Pending - esperando aprobación)
│   └── AdminEmpresa (no puede hacer login)
│
└── EMPRESA C (Rejected)
    └── AdminEmpresa (bloqueado)
```

**Aislamiento de Datos:**
- Cada empresa tiene sus propios usuarios, tareas, capacidades y chats
- Los datos están completamente aislados por `empresaId`
- AdminGeneral es el único que puede ver/gestionar múltiples empresas

---

## 👥 LOS 4 ROLES Y SUS CAPACIDADES

### 1. AdminGeneral (Rol = 1)

**¿Quién es?**
- Super administrador de la plataforma
- NO pertenece a ninguna empresa (`empresaId = null`)
- Puede haber múltiples AdminGeneral

**¿Qué puede hacer?**
- ✅ Ver todas las empresas registradas
- ✅ Aprobar/rechazar solicitudes de registro de empresas
- ✅ Eliminar empresas completamente (hard delete)
- ✅ Ver estadísticas de cualquier empresa
- ✅ Crear otros AdminGeneral

**¿Qué NO puede hacer?**
- ❌ Crear/gestionar tareas (no pertenece a ninguna empresa)
- ❌ Chatear con Workers o Managers (solo con AdminEmpresa)
- ❌ Acceder a tareas específicas de empresas

**Flujo de Uso:**
1. Recibe notificación de nueva empresa registrada
2. Revisa información de la empresa
3. Aprueba o rechaza el registro
4. Monitorea estadísticas globales

**Endpoints Clave:**
```
GET    /api/empresas                    # Listar empresas (filtrar por estado)
PUT    /api/empresas/{id}/aprobar       # Aprobar empresa
PUT    /api/empresas/{id}/rechazar      # Rechazar empresa
DELETE /api/empresas/{id}               # Eliminar empresa
GET    /api/empresas/{id}/estadisticas  # Ver stats de empresa
```

---

### 2. AdminEmpresa (Rol = 2)

**¿Quién es?**
- Dueño/administrador de UNA empresa específica
- Se crea automáticamente al registrar una empresa
- Pertenece a una empresa (`empresaId != null`)

**¿Qué puede hacer?**
- ✅ Gestionar TODOS los usuarios de su empresa (crear, editar, eliminar)
- ✅ Crear tareas para CUALQUIER departamento de su empresa
- ✅ Asignar tareas manual o automáticamente
- ✅ Ver TODAS las tareas de su empresa
- ✅ Gestionar capacidades (skills) de la empresa
- ✅ Ver estadísticas de su empresa
- ✅ Chatear con CUALQUIER persona de su empresa + AdminGeneral

**¿Qué NO puede hacer?**
- ❌ Ver/gestionar otras empresas
- ❌ Aprobar su propia empresa (debe esperar a AdminGeneral)
- ❌ Modificar su propio rol

**Flujo de Registro:**
```
1. POST /api/auth/register-adminempresa
   └─> Crea Empresa (estado: Pending) + AdminEmpresa

2. Espera aprobación de AdminGeneral

3. AdminGeneral: PUT /api/empresas/{id}/aprobar
   └─> Empresa.Estado = Approved

4. AdminEmpresa puede hacer login
   └─> POST /api/auth/login
```

**Flujo de Setup de Empresa:**
```dart
// 1. Crear Managers de Departamentos
POST /api/usuarios {
  "rol": "ManagerDepartamento",
  "departamento": "Produccion",
  ...
}

// 2. Crear Workers
POST /api/usuarios {
  "rol": "Usuario",
  "departamento": "Produccion",
  ...
}

// 3. Definir Capacidades (skills)
PUT /api/usuarios/{userId}/capacidades {
  "capacidades": [
    {"nombre": "Carpinteria", "nivel": 4},
    {"nombre": "Electricidad", "nivel": 3}
  ]
}

// 4. Crear primera tarea
POST /api/tareas {
  "titulo": "Reparar máquina X",
  "departamento": "Produccion",
  "capacidadesRequeridas": ["Carpinteria"]
}

// 5. Asignar tarea
PUT /api/tareas/{id}/asignar-automatico
```

---

### 3. ManagerDepartamento (Rol = 4)

**¿Quién es?**
- Jefe de un departamento específico
- Tiene doble rol: gestor + trabajador
- Pertenece a empresa + departamento específico

**Departamentos Disponibles:**
```csharp
enum Departamento {
  Ninguno = 0,
  Finanzas = 1,
  Mantenimiento = 2,
  Produccion = 3,
  Marketing = 4,
  Logistica = 5
}
```

**¿Qué puede hacer?**
- ✅ Crear tareas SOLO para su departamento
- ✅ Asignar workers SOLO de su departamento
- ✅ Ver tareas asignadas A ÉL (como worker)
- ✅ Delegar tareas a otros ManagerDepartamento
- ✅ Aceptar/rechazar delegaciones recibidas
- ✅ Aceptar y completar tareas (como worker)
- ✅ Chatear con: AdminEmpresa, otros Managers, Workers de su dept

**¿Qué NO puede hacer?**
- ❌ Crear tareas para otros departamentos
- ❌ Asignar workers de otros departamentos
- ❌ Ver tareas de otros workers (solo las suyas)
- ❌ Chatear con workers de otros departamentos

**Restricciones Críticas:**

```typescript
// ❌ INCORRECTO - Manager de Producción NO puede hacer esto:
POST /api/tareas {
  "departamento": "Finanzas"  // Error: no es su departamento
}

PUT /api/tareas/{id}/asignar-manual {
  "usuarioId": "worker-finanzas-id"  // Error: worker de otro dept
}

// ✅ CORRECTO
POST /api/tareas {
  "departamento": "Produccion"  // ✓ Su departamento
}

PUT /api/tareas/{id}/asignar-manual {
  "usuarioId": "worker-produccion-id"  // ✓ Worker de su dept
}
```

**Flujo de Delegación (IMPORTANTE):**

```
ESCENARIO:
Manager de Producción recibe tarea que requiere skills de Mantenimiento

1. Manager Producción: "Esta tarea es más de Mantenimiento"
   └─> PUT /api/tareas/{id}/delegar
       {
         "jefeDestinoId": "manager-mantenimiento-id",
         "comentario": "Requiere skills de plomería"
       }

2. Estado de la tarea:
   ├─> estaDelegada = true
   ├─> delegadoPorUsuarioId = manager-produccion-id
   ├─> delegadoAUsuarioId = manager-mantenimiento-id
   ├─> delegacionAceptada = null (PENDIENTE)
   └─> departamento cambia a "Mantenimiento"

3. Manager Mantenimiento VE la tarea en estado pendiente
   └─> GET /api/tareas/mis
       {
         "estaDelegada": true,
         "delegacionAceptada": null  // Debe responder
       }

4. Manager Mantenimiento DECIDE:

   OPCIÓN A - ACEPTAR:
   PUT /api/tareas/{id}/aceptar-delegacion {
     "comentario": "Ok, la asignaré a mi equipo"
   }
   └─> delegacionAceptada = true
   └─> Ahora puede asignar a workers de Mantenimiento

   OPCIÓN B - RECHAZAR:
   PUT /api/tareas/{id}/rechazar-delegacion {
     "motivoRechazo": "Mi equipo está sobrecargado esta semana"
   }
   └─> delegacionAceptada = false
   └─> Tarea regresa a Manager Producción
   └─> departamento vuelve a "Produccion"
```

---

### 4. Usuario (Worker/Empleado) (Rol = 3)

**¿Quién es?**
- Trabajador que ejecuta tareas
- Pertenece a empresa + departamento específico
- NO puede crear ni asignar tareas

**¿Qué puede hacer?**
- ✅ Ver SOLO sus tareas asignadas
- ✅ Aceptar tareas asignadas
- ✅ Completar tareas con evidencia (texto + imagen)
- ✅ Ver su dashboard de estadísticas
- ✅ Actualizar sus propias capacidades (skills)
- ✅ Chatear con cualquier persona de su empresa

**¿Qué NO puede hacer?**
- ❌ Ver tareas de otros workers
- ❌ Crear tareas
- ❌ Asignar tareas
- ❌ Delegar tareas
- ❌ Ver información de la empresa (solo su perfil)

**Límite de Tareas:**
```
MAX_TAREAS_ACTIVAS = 5 (configurable en appsettings.json)

Tareas activas = Estado "Asignada" OR "Aceptada"

Si worker tiene 5 tareas activas:
  - No se le pueden asignar más tareas
  - Debe completar/cancelar tareas primero
```

**Flujo de Trabajo:**

```
1. Worker recibe notificación (SignalR):
   ├─> Evento: "tarea:assigned"
   └─> Tarea.Estado = "Asignada"

2. Worker VE la tarea:
   GET /api/tareas/mis
   └─> Filtra solo tareas asignadas a él

3. Worker ACEPTA la tarea:
   PUT /api/tareas/{id}/aceptar
   └─> Estado cambia a "Aceptada"

4. Worker TRABAJA en la tarea...

5. Worker COMPLETA la tarea:
   PUT /api/tareas/{id}/finalizar {
     "evidenciaTexto": "Reparé el motor y probé funcionamiento",
     "evidenciaImagenUrl": "https://storage.com/evidence.jpg"
   }
   └─> Estado cambia a "Finalizada"
   └─> Evidencia guardada
   └─> finalizadaAt = timestamp actual
```

**Capacidades (Skills) del Worker:**

```dart
// Worker puede actualizar sus propias skills
PUT /api/usuarios/mis-capacidades {
  "capacidades": [
    {
      "capacidadId": "existing-skill-id",  // Skill existente
      "nivel": 4  // Nivel 1-5
    }
  ]
}

// Worker puede eliminar una skill de su perfil
DELETE /api/usuarios/mis-capacidades/{capacidadId}
```

---

## 📋 SISTEMA DE TAREAS

### Estados de Tarea

```typescript
enum EstadoTarea {
  Pendiente = 0,   // Creada, sin asignar
  Asignada = 1,    // Asignada a worker, esperando aceptación
  Aceptada = 2,    // Worker aceptó, trabajando en ella
  Finalizada = 3,  // Completada con evidencia
  Cancelada = 4    // Cancelada por admin/manager
}
```

### Prioridades

```typescript
enum PrioridadTarea {
  Low = 0,
  Medium = 1,
  High = 2
}
```

### Ciclo de Vida Completo

```
┌─────────────────────────────────────────────────────────────┐
│ CREACIÓN (AdminEmpresa o ManagerDepartamento)               │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
                  [Pendiente]
                        │
        ┌───────────────┼───────────────┐
        │               │               │
        ▼               ▼               ▼
   MANUAL          AUTOMÁTICA       DELEGACIÓN
  (Admin/Mgr)      (Algoritmo)    (Manager→Manager)
        │               │               │
        │               │               ▼
        │               │          ┌─────────┐
        │               │          │Pendiente│
        │               │          │Aprobación│
        │               │          └────┬────┘
        │               │               │
        │               │      ┌────────┴────────┐
        │               │      │                 │
        │               │    ACEPTAR         RECHAZAR
        │               │      │                 │
        │               │      ▼                 ▼
        └───────┬───────┴─>[Asignada]      [Regresa al
                │                           Manager origen]
                ▼
          Worker acepta
                │
                ▼
            [Aceptada]
                │
                ▼
       Worker completa
       (evidencia req.)
                │
                ▼
           [Finalizada]

        (En cualquier momento)
                │
                ▼
          CANCELAR
          (Admin/Mgr)
                │
                ▼
           [Cancelada]
```

---

## 🤖 ASIGNACIÓN AUTOMÁTICA DE TAREAS

### Algoritmo de Asignación Inteligente

Cuando se llama `PUT /api/tareas/{id}/asignar-automatico`, el sistema:

**1. Filtra Candidatos:**
```sql
SELECT * FROM Usuarios
WHERE Departamento = tarea.Departamento
  AND IsActive = true
  AND Rol = 'Usuario'
  AND (tiene TODAS las capacidadesRequeridas)
```

**2. Calcula Score para cada candidato:**
```csharp
int baseScore = 100;
int tareasActivas = CountActiveTasks(worker);
int nivelHabilidad = worker.NivelHabilidad; // 1-5

int score = baseScore
            - (tareasActivas * 20)      // Penaliza carga actual
            + (nivelHabilidad * 5);     // Recompensa nivel alto

// Bonificaciones
if (HasPerfectSkillMatch(worker, tarea))
    score += 30;

if (tarea.Prioridad == High && tareasActivas == 0)
    score += 50;  // Trabajador libre para urgente
```

**3. Ejemplo de Scoring:**

```
TAREA: "Reparar máquina" (Prioridad: High, Skills: Mecánica, Electricidad)

Worker A: 2 tareas, nivel 4, tiene skills perfectos
  = 100 - (2×20) + (4×5) + 30
  = 100 - 40 + 20 + 30
  = 110

Worker B: 0 tareas, nivel 3, tiene skills perfectos
  = 100 - 0 + (3×5) + 30 + 50 (bonus urgente + libre)
  = 100 + 15 + 30 + 50
  = 195  ← SELECCIONADO

Worker C: 5 tareas, nivel 5, skills perfectos
  = Descartado (ya tiene MAX_TAREAS)
```

**4. Validaciones:**
- Si worker ya tiene 5 tareas activas → SKIP
- Si ningún worker califica → Error 400

**5. Resultado:**
```json
{
  "success": true,
  "message": "Asignación automática ejecutada",
  "data": {
    "asignadoA": "Worker B",
    "score": 195,
    "motivoAsignacion": "Score: 195"
  }
}
```

---

## 🔄 REASIGNACIÓN DE TAREAS

### Cuándo y Cómo Reasignar

**Escenarios Comunes:**
1. Worker está de vacaciones
2. Worker renunció
3. Tarea fue asignada incorrectamente
4. Urgencia cambió

**Endpoint:**
```http
PUT /api/tareas/{tareaId}/reasignar
{
  // OPCIÓN 1: Reasignar a worker específico
  "nuevoUsuarioId": "worker-2-id",
  "motivo": "Worker original está de vacaciones"

  // OPCIÓN 2: Reasignar automáticamente
  "asignacionAutomatica": true,
  "motivo": "Worker original renunció"
}
```

**Proceso:**
1. Limpia asignación actual (`AsignadoAUsuarioId = null`)
2. Cambia estado a `Pendiente`
3. Si `nuevoUsuarioId`: Asigna a ese worker específico
4. Si `asignacionAutomatica`: Ejecuta algoritmo de auto-asignación
5. Registra en historial (tipo: `Reasignacion`)

**Historial de Asignaciones:**
```http
GET /api/tareas/{id}/historial-asignaciones

Response:
[
  {
    "asignadoAUsuarioNombre": "Worker 2",
    "tipoAsignacion": "Reasignacion",
    "motivo": "Worker original de vacaciones",
    "fechaAsignacion": "2024-01-05T10:00:00Z"
  },
  {
    "asignadoAUsuarioNombre": "Worker 1",
    "tipoAsignacion": "Automatica",
    "motivo": "Score: 195",
    "fechaAsignacion": "2024-01-01T08:00:00Z"
  }
]
```

---

## 💬 SISTEMA DE CHAT EN TIEMPO REAL

### Tecnología: SignalR (WebSockets)

**URL de Conexión:**
```
wss://api.taskcontrol.work/apphub?access_token={JWT_TOKEN}
```

### Tipos de Chat

```typescript
enum ChatType {
  OneToOne = 0,  // Chat directo 1:1
  Group = 1      // Chat grupal (3+ personas)
}
```

### Matriz de Permisos de Chat

| Rol Usuario            | Puede chatear con...                                    |
|------------------------|---------------------------------------------------------|
| AdminGeneral           | ❗ SOLO AdminEmpresa (restringido)                      |
| AdminEmpresa           | ✅ Cualquier persona de su empresa + AdminGeneral       |
| ManagerDepartamento    | ✅ AdminEmpresa<br>✅ Otros Managers de su empresa<br>✅ Workers DE SU DEPARTAMENTO solamente |
| Usuario (Worker)       | ✅ Cualquier persona de su empresa                      |

**Validaciones Críticas:**

```typescript
// ❌ NO PERMITIDO
AdminGeneral → Usuario (worker)
AdminGeneral → ManagerDepartamento
Usuario Empresa A → Usuario Empresa B

ManagerDepartamento (Producción) → Worker (Finanzas)

// ✅ PERMITIDO
AdminGeneral → AdminEmpresa
AdminEmpresa → Cualquiera de su empresa
ManagerDepartamento (Producción) → Worker (Producción)
Usuario → Usuario (misma empresa)
```

### Flujo de Creación de Chat

**Chat 1:1:**
```http
POST /api/chats/one-to-one
{
  "userId": "other-user-id"
}

Response:
{
  "id": "chat-id"  // Si ya existe, devuelve el existente
}
```

**Chat Grupal:**
```http
POST /api/chats/group
{
  "name": "Equipo Proyecto Alpha",
  "memberIds": ["user1", "user2", "user3"]
}

Response:
{
  "id": "group-chat-id"
}
```

### Envío y Recepción de Mensajes

**Enviar Mensaje (REST API):**
```http
POST /api/chats/{chatId}/messages
{
  "text": "Hola equipo!"
}

Response:
{
  "id": "message-id",
  "body": "Hola equipo!",
  "createdAt": "2024-01-01T12:00:00Z",
  "senderId": "my-user-id",
  "chatId": "chat-id"
}
```

**Recibir Mensaje (SignalR Event):**
```javascript
// TODOS los miembros del chat reciben:
connection.on("chat:message", (payload) => {
  console.log(payload);
  // {
  //   id: "message-id",
  //   body: "Hola equipo!",
  //   createdAt: "2024-01-01T12:00:00Z",
  //   senderId: "user-id",
  //   chatId: "chat-id"
  // }
});
```

### Grupos de SignalR

**1. Chat Groups (mensajería):**
```javascript
// Unirse a un chat específico
await connection.invoke("JoinChat", chatId);

// Eventos que recibes:
connection.on("chat:message", handler);
```

**2. Empresa Groups (notificaciones de tareas):**
```javascript
// Unirse al grupo de tu empresa
await connection.invoke("JoinEmpresaGroup", empresaId);

// Eventos que recibes:
connection.on("tarea:created", handler);
connection.on("tarea:assigned", handler);
connection.on("tarea:accepted", handler);
connection.on("tarea:completed", handler);
```

**3. Department Groups (notificaciones de departamento):**
```javascript
// Unirse al grupo de tu departamento
await connection.invoke("JoinDepartmentGroup", empresaId, "Produccion");

// Eventos específicos del departamento
```

**4. Super Admin Group:**
```javascript
// Solo para AdminGeneral
await connection.invoke("JoinSuperAdminGroup");

// Notificaciones de nuevas empresas, etc.
```

### Mensajes No Leídos

```http
# Obtener total de mensajes no leídos
GET /api/chat/unread-count
{
  "unreadCount": 15
}

# Obtener no leídos por chat (para badges)
GET /api/chat/unread-by-chat
{
  "chat-id-1": 5,
  "chat-id-2": 10,
  "chat-id-3": 0
}

# Marcar mensaje como leído
PUT /api/chat/messages/{messageId}/mark-read

# Marcar todos los mensajes de un chat como leídos
PUT /api/chat/{chatId}/mark-all-read
```

---

## 🔐 AUTENTICACIÓN Y SEGURIDAD

### Sistema JWT con Refresh Tokens

**Tokens:**
- **Access Token**: JWT válido por 30 minutos (configurable)
- **Refresh Token**: Token opaco válido por 7 días (configurable)

**Flujo de Login:**
```http
POST /api/auth/login
{
  "email": "user@company.com",
  "password": "Password123!"
}

Response 200:
{
  "success": true,
  "message": "Login exitoso",
  "data": {
    "tokens": {
      "accessToken": "eyJhbGciOiJIUzI1NiIs...",
      "refreshToken": "base64-encoded-token",
      "expiresIn": 1800  // segundos (30 min)
    },
    "usuario": {
      "id": "guid",
      "email": "user@company.com",
      "nombreCompleto": "John Doe",
      "rol": "AdminEmpresa",
      "empresaId": "guid",
      "departamento": null
    }
  }
}
```

**Contenido del JWT (Claims):**
```json
{
  "sub": "user-id-guid",           // Subject (userId)
  "role": "AdminEmpresa",          // Rol del usuario
  "empresaId": "empresa-id-guid",  // ID de empresa (null para AdminGeneral)
  "email": "user@company.com",
  "exp": 1704123600                // Expiration timestamp
}
```

**Uso del Access Token:**
```http
GET /api/tareas/mis
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### Refresh Token Flow (Crítico)

**Cuándo Refrescar:**
- Cuando Access Token expira (401 Unauthorized)
- Proactivamente antes de expirar (recomendado)

**Proceso:**
```http
POST /api/auth/refresh
{
  "refreshToken": "current-refresh-token"
}

Response 200:
{
  "success": true,
  "data": {
    "accessToken": "NEW-JWT-TOKEN",
    "refreshToken": "NEW-REFRESH-TOKEN",  // ⚠️ TOKEN ROTADO
    "expiresIn": 1800
  }
}
```

**⚠️ IMPORTANTE - ROTACIÓN DE TOKENS:**
```
El refresh token SE ROTA en cada uso:
1. Cliente envía refreshToken A
2. Backend genera nuevos tokens
3. Backend REVOCA refreshToken A (ya no es válido)
4. Backend devuelve:
   - Nuevo accessToken
   - Nuevo refreshToken B
5. Cliente DEBE guardar refreshToken B
6. Si intenta usar refreshToken A de nuevo → ERROR 401
```

**Implementación Flutter Recomendada:**
```dart
class AuthInterceptor extends Interceptor {
  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode == 401) {
      try {
        // 1. Obtener refresh token actual
        final refreshToken = await storage.read(key: 'refresh_token');

        // 2. Llamar a refresh endpoint
        final response = await dio.post('/api/auth/refresh',
          data: {'refreshToken': refreshToken}
        );

        // 3. Guardar NUEVOS tokens (no los viejos!)
        final newAccess = response.data['data']['accessToken'];
        final newRefresh = response.data['data']['refreshToken'];

        await storage.write(key: 'access_token', value: newAccess);
        await storage.write(key: 'refresh_token', value: newRefresh);

        // 4. Reintentar request original con nuevo token
        err.requestOptions.headers['Authorization'] = 'Bearer $newAccess';
        final retryResponse = await dio.fetch(err.requestOptions);
        return handler.resolve(retryResponse);

      } catch (e) {
        // Refresh falló, hacer logout
        await logout();
        return handler.reject(err);
      }
    }
    return handler.next(err);
  }
}
```

### Logout

```http
POST /api/auth/logout
Authorization: Bearer {access-token}
{
  "refreshToken": "current-refresh-token"
}

Response 200:
{
  "success": true,
  "message": "Logout OK"
}
```

**Proceso:**
1. Revoca el refresh token en BD (marca `IsRevoked = true`)
2. Access token sigue válido hasta expirar (no se puede revocar)
3. Cliente debe eliminar tokens localmente

---

## 🎓 SISTEMA DE CAPACIDADES (SKILLS)

### ¿Qué son las Capacidades?

Las capacidades son **habilidades/skills** que:
- Pertenecen a una empresa específica (`empresaId`)
- Se definen por nombre (ej: "Carpintería", "Diseño Gráfico", "Plomería")
- Los workers las vinculan con un nivel de proficiencia (1-5)

### Flujo de Uso

**1. Crear Capacidades (AdminEmpresa):**
```http
# Se crean automáticamente al asignar a usuario
PUT /api/usuarios/{userId}/capacidades
{
  "capacidades": [
    {
      "nombre": "Carpintería",  // Auto-crea la capacidad
      "nivel": 4
    },
    {
      "nombre": "Electricidad",
      "nivel": 3
    }
  ]
}
```

**2. Workers Actualizan Sus Capacidades:**
```http
PUT /api/usuarios/mis-capacidades
{
  "capacidades": [
    {
      "capacidadId": "existing-capacidad-id",
      "nivel": 5  // Mejoró su nivel
    }
  ]
}
```

**3. Tareas Requieren Capacidades:**
```http
POST /api/tareas
{
  "titulo": "Reparar mueble de oficina",
  "capacidadesRequeridas": [
    "Carpintería",
    "Pintura"
  ]
}
```

**4. Validación en Asignación:**
```
Worker tiene: ["Carpintería": 4, "Electricidad": 3]
Tarea requiere: ["Carpintería", "Pintura"]

Resultado: ❌ FALLA - Worker no tiene "Pintura"

Soluciones:
1. Asignar a otro worker
2. Usar ignorarValidacionesSkills: true
3. Worker aprende "Pintura" primero
```

---

## 📊 ESTADÍSTICAS Y DASHBOARDS

### Estadísticas de Empresa

```http
GET /api/empresas/{empresaId}/estadisticas

Response:
{
  "empresaId": "guid",
  "nombreEmpresa": "Acme Corp",
  "totalTrabajadores": 50,
  "trabajadoresActivos": 48,
  "totalTareas": 200,
  "tareasPendientes": 20,
  "tareasAsignadas": 30,
  "tareasAceptadas": 50,
  "tareasFinalizadas": 90,
  "tareasCanceladas": 10
}
```

### Dashboard de Usuario (Worker/Manager)

```http
GET /api/usuarios/me/dashboard

Response:
{
  "tareas": {
    "total": 12,
    "pendientes": 0,
    "asignadas": 2,
    "aceptadas": 5,
    "finalizadas": 5,
    "hoy": 3,       // Vencen hoy
    "urgentes": 1   // Alta prioridad
  }
}
```

### Tareas Recientes de Usuario

```http
GET /api/usuarios/me/tareas-recientes?limit=10

Response:
[
  {
    "id": "guid",
    "titulo": "Reparar conveyor belt",
    "estado": "Aceptada",
    "prioridad": "High",
    "dueDate": "2024-01-15T00:00:00Z",
    "createdByNombre": "Admin Name",
    "createdAt": "2024-01-01T00:00:00Z"
  }
]
```

---

## 🗄️ MODELO DE BASE DE DATOS

### Entidades Principales

**Empresa**
```csharp
{
  Id: Guid,
  Nombre: string,
  Direccion: string (nullable),
  Telefono: string (nullable),
  Estado: EstadoEmpresa (Pending/Approved/Rejected),
  IsActive: bool,  // Soft delete
  CreatedAt: DateTime
}
```

**Usuario**
```csharp
{
  Id: Guid,
  Email: string (unique),
  NombreCompleto: string,
  Telefono: string (nullable),
  PasswordHash: byte[],
  PasswordSalt: byte[],
  Rol: RolUsuario (1=AdminGeneral, 2=AdminEmpresa, 3=Usuario, 4=ManagerDept),
  EmpresaId: Guid (nullable, null para AdminGeneral),
  Departamento: Departamento (nullable),
  NivelHabilidad: int (1-5, nullable),
  IsActive: bool,  // Soft delete
  CreatedAt: DateTime
}
```

**Tarea**
```csharp
{
  Id: Guid,
  EmpresaId: Guid,
  Titulo: string,
  Descripcion: string,
  Estado: EstadoTarea,
  Prioridad: PrioridadTarea,
  DueDate: DateTime (nullable),
  Departamento: Departamento (nullable),

  // Asignación
  AsignadoAUsuarioId: Guid (nullable),
  CreatedByUsuarioId: Guid,

  // Delegación
  EstaDelegada: bool,
  DelegadoPorUsuarioId: Guid (nullable),
  DelegadoAUsuarioId: Guid (nullable),
  DelegadaAt: DateTime (nullable),
  DelegacionAceptada: bool (nullable),  // null=pendiente, true/false
  MotivoRechazoJefe: string (nullable),
  DelegacionResueltaAt: DateTime (nullable),

  // Evidencia
  EvidenciaTexto: string (nullable),
  EvidenciaImagenUrl: string (nullable),
  FinalizadaAt: DateTime (nullable),

  // Cancelación
  MotivoCancelacion: string (nullable),

  IsActive: bool,
  CreatedAt: DateTime,
  UpdatedAt: DateTime (nullable)
}
```

**TareaCapacidadRequerida**
```csharp
{
  Id: Guid,
  TareaId: Guid,
  Nombre: string  // Nombre de la skill (no FK)
}
```

**Capacidad**
```csharp
{
  Id: Guid,
  EmpresaId: Guid,
  Nombre: string,
  IsActive: bool,
  CreatedAt: DateTime
}
```

**UsuarioCapacidad**
```csharp
{
  UsuarioId: Guid (PK),
  CapacidadId: Guid (PK),
  Nivel: int (1-5)
}
```

**Chat**
```csharp
{
  Id: Guid,
  Type: ChatType (OneToOne/Group),
  Name: string (nullable, max 128),
  CreatedById: Guid (nullable),
  CreatedAt: DateTimeOffset
}
```

**ChatMember**
```csharp
{
  ChatId: Guid (PK),
  UserId: Guid (PK),
  Role: ChatRole (Member/Owner),
  JoinedAt: DateTimeOffset
}
```

**Message**
```csharp
{
  Id: Guid,
  ChatId: Guid,
  SenderId: Guid,
  Body: string (max 4000),
  CreatedAt: DateTimeOffset,
  IsRead: bool,
  ReadAt: DateTimeOffset (nullable)
}
```

**RefreshToken**
```csharp
{
  Id: Guid,
  UsuarioId: Guid,
  TokenHash: string (SHA256),
  ExpiresAt: DateTime,
  CreatedAt: DateTime,
  IsRevoked: bool,
  RevokedAt: DateTime (nullable),
  RevokeReason: string (nullable)
}
```

---

## 🚀 EVENTOS DE SIGNALR EN TIEMPO REAL

### Eventos de Tareas

```javascript
// Tarea creada
connection.on("tarea:created", (data) => {
  // { id, titulo, empresaId, estado, prioridad, departamento, createdAt }
});

// Tarea asignada
connection.on("tarea:assigned", (data) => {
  // { id, titulo, empresaId, estado, asignadoAUsuarioId, asignadoANombre, updatedAt }
});

// Tarea aceptada
connection.on("tarea:accepted", (data) => {
  // { id, titulo, empresaId, estado, asignadoAUsuarioId, updatedAt }
});

// Tarea completada
connection.on("tarea:completed", (data) => {
  // { id, titulo, empresaId, estado, asignadoAUsuarioId, finalizadaAt, updatedAt }
});

// Tarea reasignada
connection.on("tarea:reasignada", (data) => {
  // { id, titulo, empresaId, usuarioAnteriorId, nuevoUsuarioId, motivo, updatedAt }
});
```

### Eventos de Chat

```javascript
// Nuevo mensaje
connection.on("chat:message", (data) => {
  // { id, body, createdAt, senderId, chatId }
});
```

---

## ✅ CHECKLIST PARA FLUTTER DEVELOPER

### Antes de Empezar
- [ ] Leer este documento completo
- [ ] Entender los 4 roles y sus permisos
- [ ] Comprender flujo de delegación de tareas
- [ ] Revisar matriz de permisos de chat
- [ ] Entender refresh token rotation

### Setup Inicial
- [ ] Configurar `dio` para HTTP requests
- [ ] Implementar interceptor de autenticación
- [ ] Implementar auto-refresh de tokens
- [ ] Configurar `flutter_secure_storage` para tokens
- [ ] Configurar SignalR (`signalr_netcore`)

### Implementación por Rol

**AdminGeneral:**
- [ ] Pantalla de lista de empresas (filtros: Pending/Approved/Rejected)
- [ ] Botones de aprobar/rechazar empresa
- [ ] Estadísticas de empresa
- [ ] Chat solo con AdminEmpresa

**AdminEmpresa:**
- [ ] Dashboard de empresa
- [ ] CRUD de usuarios (managers y workers)
- [ ] CRUD de tareas
- [ ] Asignación manual/automática
- [ ] Gestión de capacidades
- [ ] Chat con todos de su empresa

**ManagerDepartamento:**
- [ ] Crear tareas SOLO de su departamento
- [ ] Asignar workers SOLO de su departamento
- [ ] Ver tareas delegadas pendientes
- [ ] Aceptar/rechazar delegaciones
- [ ] Delegar tareas a otros managers
- [ ] Chat con AdminEmpresa, managers, workers de su dept

**Usuario (Worker):**
- [ ] Ver solo sus tareas asignadas
- [ ] Aceptar tareas
- [ ] Completar tareas con evidencia
- [ ] Upload de imágenes a storage
- [ ] Actualizar sus capacidades
- [ ] Chat con todos de su empresa

### Features Comunes
- [ ] SignalR connection management
- [ ] Notificaciones push (opcional)
- [ ] Modo offline (caché)
- [ ] Pull-to-refresh
- [ ] Paginación infinita
- [ ] Búsqueda y filtros
- [ ] Manejo de errores global

---

## 📞 ENDPOINTS COMPLETOS POR CATEGORÍA

### Auth
```
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout
POST   /api/auth/register-adminempresa
POST   /api/auth/register-admingeneral
```

### Empresas
```
GET    /api/empresas
GET    /api/empresas/{id}/estadisticas
GET    /api/empresas/{id}/trabajadores-ids
PUT    /api/empresas/{id}/aprobar
PUT    /api/empresas/{id}/rechazar
DELETE /api/empresas/{id}
```

### Usuarios
```
GET    /api/usuarios/me
GET    /api/usuarios/me/dashboard
GET    /api/usuarios/me/tareas-recientes
GET    /api/usuarios
GET    /api/usuarios/{id}
POST   /api/usuarios
PUT    /api/usuarios/{id}
DELETE /api/usuarios/{id}
PUT    /api/usuarios/{id}/capacidades
PUT    /api/usuarios/mis-capacidades
DELETE /api/usuarios/mis-capacidades/{capacidadId}
```

### Tareas
```
POST   /api/tareas
GET    /api/tareas
GET    /api/tareas/mis
GET    /api/tareas/{id}
PUT    /api/tareas/{id}/asignar-manual
PUT    /api/tareas/{id}/asignar-automatico
PUT    /api/tareas/{id}/aceptar
PUT    /api/tareas/{id}/finalizar
PUT    /api/tareas/{id}/cancelar
PUT    /api/tareas/{id}/reasignar
PUT    /api/tareas/{id}/delegar
PUT    /api/tareas/{id}/aceptar-delegacion
PUT    /api/tareas/{id}/rechazar-delegacion
GET    /api/tareas/{id}/historial-asignaciones
```

### Chat
```
GET    /api/users/search
GET    /api/chats
POST   /api/chats/one-to-one
POST   /api/chats/group
POST   /api/chats/{chatId}/members
GET    /api/chats/{chatId}/messages
POST   /api/chats/{chatId}/messages
PUT    /api/chat/messages/{messageId}/mark-read
PUT    /api/chat/{chatId}/mark-all-read
GET    /api/chat/unread-count
GET    /api/chat/unread-by-chat
```

### SignalR Hub Methods
```
JoinChat(chatId)
LeaveChat(chatId)
JoinEmpresaGroup(empresaId)
LeaveEmpresaGroup(empresaId)
JoinDepartmentGroup(empresaId, departamento)
LeaveDepartmentGroup(empresaId, departamento)
JoinSuperAdminGroup()
LeaveSuperAdminGroup()
```

---

## 🎯 CASOS DE USO CRÍTICOS A IMPLEMENTAR

### 1. Flujo de Registro de Nueva Empresa
```
1. Usuario abre app
2. Selecciona "Registrar mi empresa"
3. Completa formulario (nombre empresa, datos admin)
4. POST /api/auth/register-adminempresa
5. Muestra "Esperando aprobación de administrador"
6. [AdminGeneral aprueba desde su panel]
7. Usuario puede hacer login
```

### 2. Flujo de Delegación entre Managers
```
1. Manager A recibe tarea de AdminEmpresa
2. Manager A se da cuenta que es de otro departamento
3. Busca Manager del departamento correcto
4. Delega tarea con comentario
5. Manager B recibe notificación (SignalR)
6. Manager B ve tarea en "Pendientes de aprobación"
7. Manager B acepta o rechaza
8. Si acepta: puede asignar a sus workers
9. Si rechaza: vuelve a Manager A
```

### 3. Flujo de Asignación Automática
```
1. AdminEmpresa crea tarea urgente
2. Especifica capacidades requeridas
3. Click "Asignar automáticamente"
4. Sistema calcula scores de workers
5. Asigna al mejor candidato
6. Worker recibe notificación en tiempo real
7. Worker abre app y ve nueva tarea
8. Worker acepta y comienza a trabajar
```

### 4. Flujo de Finalización con Evidencia
```
1. Worker completa el trabajo físico
2. Abre app, va a tarea
3. Click "Completar tarea"
4. Escribe descripción de lo realizado
5. Toma foto de evidencia
6. [Flutter] Sube imagen a Firebase Storage
7. Obtiene URL de imagen
8. Envía evidenciaTexto + evidenciaImagenUrl
9. Backend marca tarea como Finalizada
10. AdminEmpresa recibe notificación
```

### 5. Flujo de Chat en Tiempo Real
```
1. Usuario A busca Usuario B
2. Crea chat 1:1
3. [SignalR] JoinChat(chatId)
4. Usuario A escribe mensaje
5. POST /api/chats/{chatId}/messages
6. Backend emite evento "chat:message"
7. Usuario B recibe mensaje en tiempo real
8. Usuario B responde
9. Ambos ven conversación actualizada
```

---

## 🔧 CONFIGURACIÓN BACKEND

### appsettings.json Crítico

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=TaskControl;..."
  },
  "JwtSettings": {
    "SecretKey": "tu-secreto-de-32-caracteres-minimo",
    "Issuer": "TaskControlAPI",
    "Audience": "TaskControlClient",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "AppSettings": {
    "MaxTareasActivasPorUsuario": 5
  }
}
```

### CORS (Importante para Flutter Web)

```csharp
Origins permitidos:
- http://localhost:*
- https://localhost:*
- https://taskcontrol.work

Policies:
- AllowAnyHeader()
- AllowAnyMethod()
- AllowCredentials()  // Requerido para SignalR
```

---

## 📝 NOTAS FINALES IMPORTANTES

### 1. Seguridad de Passwords
- Backend requiere mínimo 6 caracteres
- Recomienda validar en Flutter: 8+ chars, mayúsculas, minúsculas, números

### 2. Manejo de Imágenes
- Backend NO sube imágenes
- Espera URLs (ej: Firebase Storage, AWS S3)
- Flutter debe:
  1. Subir imagen a storage
  2. Obtener URL pública
  3. Enviar URL al backend

### 3. Fechas y Timezone
- Backend usa UTC
- Flutter debe convertir a timezone local para display
- Al enviar: convertir a UTC

### 4. Paginación
- No implementada en todos los endpoints
- Implementar en Flutter con `skip` y `take` donde esté disponible

### 5. Soft Deletes
- `Empresa.IsActive` y `Usuario.IsActive`
- Eliminados no aparecen en queries
- NO se pueden reactivar desde API (solo DB directa)

---

## 🎉 PRÓXIMOS PASOS

1. **Leer esta documentación completa**
2. **Crear estructura de carpetas Flutter** (siguiente paso)
3. **Configurar dependencias** (dio, signalr, secure_storage)
4. **Implementar capa de autenticación**
5. **Implementar modelos de datos**
6. **Implementar servicios API**
7. **Implementar UI por rol**
8. **Integrar SignalR**
9. **Testing**
10. **Deploy**

---

**Versión del Documento:** 1.0
**Fecha:** 2024
**Backend URL:** https://api.taskcontrol.work
**Dominio:** taskcontrol.work
