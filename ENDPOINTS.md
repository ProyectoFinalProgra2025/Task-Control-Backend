# ENDPOINTS

## 1. Arquitectura y flujo

- **Stack**: ASP.NET Core 9 + EF Core SQL Server; controladores en Controllers/, servicios de dominio en Services/ y entidades en Models/, todos registrados mediante DI en Program.cs.
- **Autenticación y autorización**: JWT Bearer emitido por AuthService/TokenService, refresh tokens persistidos y políticas por rol (AdminGeneral, AdminEmpresa, ManagerDepartamento, Usuario) configuradas en Program.cs.
- **Roles del sistema**:
  - **AdminGeneral**: Superadministrador de la plataforma. Gestiona empresas y solo chatea con AdminEmpresa.
  - **AdminEmpresa**: Dueño de la empresa. Gestiona todos los departamentos, crea tareas para cualquier departamento, delega a jefes de área.
  - **ManagerDepartamento**: Jefe de un departamento específico. Crea tareas solo para su departamento, asigna workers de su departamento, puede delegar tareas a otros jefes y recibir delegaciones.
  - **Usuario**: Worker/Empleado. Acepta y ejecuta tareas asignadas por su jefe de departamento.
- **Sistema de delegación entre jefes**: Los ManagerDepartamento pueden delegar tareas a otros jefes de diferentes departamentos. El jefe destino debe aceptar o rechazar explícitamente la delegación (con motivo obligatorio en caso de rechazo). Una vez aceptada, el jefe destino toma control total de la tarea.
- **Multitenancy y validaciones**: Cada endpoint contextualizado por {empresaId} verifica el claim empresaId (métodos EmpresaCoincide) y EF aplica filtros soft-delete por entidad para aislar datos.
- **Servicios de dominio**: Clases como EmpresaService, UsuarioService, TareaService, AsignacionService, EvidenciaService, ChatService, AuditoriaService y MetricasService concentran reglas de negocio y devuelven DTOs (Dtos/) listos para la API.
- **Middlewares**: ErrorHandlingMiddleware uniforma errores (Problem Details) y IdempotencyMiddleware cachea respuestas de POST usando el header Idempotency-Key para evitar dobles ejecuciones en acciones críticas.
- **Archivos y utilidades**: pp.UseStaticFiles() expone /uploads para evidencias; helpers adicionales (paginación, PDF mínimo, hashing) viven en Helpers/.

### Ruta operativa recomendada

1. **Registro y aprobación**: un representante crea la empresa vía POST /api/empresas/registro; un AdminGlobal lista (GET /api/empresas) y aprueba o rechaza (PUT .../acciones/aprobar|rechazar).
2. **Autenticación**: los usuarios usan POST /api/auth/login para obtener ccessToken + 
efreshToken y POST /api/auth/refresh para rotarlos sin volver a exponer credenciales.
3. **Configuración de la empresa**: el AdminEmpresa propio mantiene su ficha (GET/PUT /api/empresas/{empresaId}), crea sucursales (/sucursales) y gestiona usuarios (/usuarios) asignando roles, habilidades y contraseñas.
4. **Planeación de trabajo**: supervisores generan tareas (POST /api/empresas/{empresaId}/tareas), las publican (.../acciones/publicar) y consultan listados/historial.
5. **Asignación**: se asignan trabajadores manual o automáticamente (/asignaciones/manual|auto); cada asignación pasa por aceptaciones y estados (ceptar, iniciar, pausar, inalizar, alidar).
6. **Ejecución colaborativa**: trabajadores reportan incidencias, suben evidencias, conversan en chat de tarea/global/1a1 y usan Idempotency-Key para proteger acciones repetidas.
7. **Cierre y control**: supervisores validan resultados y pueden eliminar evidencias, mientras auditores consultan Auditoria y líderes revisan Metricas para medir cumplimiento.
8. **Monitoreo continuo**: descargas CSV/PDF desde Auditoria y el resumen de Metricas alimentan tableros externos junto con los datos expuestos vía la API.

## 2. Catálogo de endpoints

### Autenticación
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/auth/login | Público | Valida email y contraseña, genera JWT + refresh token y devuelve expiraciones y rol. |
| POST | /api/auth/refresh | Público (requiere refresh válido) | Intercambia un refresh token no revocado por un nuevo par de tokens, revocando el anterior. |

### Perfil de usuario
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| GET | /api/usuarios/me | Autenticado | Devuelve ID, nombres, rol, empresa/sucursal y claims del usuario del token. |

### Empresas
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/empresas/registro | Público | Registra una empresa en estado pendiente con sus datos de contacto. |
| GET | /api/empresas | Policy AdminGlobal | Lista empresas con filtros por estado y paginación. |
| GET | /api/empresas/{empresaId} | AdminGlobal o AdminEmpresa propietario | Obtiene detalle de la empresa validando empresaId del claim. |
| PUT | /api/empresas/{empresaId}/acciones/aprobar | Policy AdminGlobal | Cambia la empresa a estado aprobada. |
| PUT | /api/empresas/{empresaId}/acciones/rechazar | Policy AdminGlobal | Marca la empresa como rechazada con su motivo. |
| PUT | /api/empresas/{empresaId} | AdminGlobal o AdminEmpresa propietario | Actualiza datos generales de la empresa. |
| DELETE | /api/empresas/{empresaId} | Policy AdminGlobal | Desactiva (soft-delete) una empresa. |

### Sucursales
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/empresas/{empresaId}/sucursales | Policy AdminEmpresa (o AdminGlobal) | Crea una sucursal dentro de la empresa validando propiedad. |
| GET | /api/empresas/{empresaId}/sucursales | Roles AdminEmpresa, Supervisor, Auditor, AdminGlobal | Lista sucursales con paginación. |
| GET | /api/empresas/{empresaId}/sucursales/{sucursalId} | Roles AdminEmpresa, Supervisor, Auditor, AdminGlobal | Detalle de una sucursal concreta. |
| PUT | /api/empresas/{empresaId}/sucursales/{sucursalId} | Policy AdminEmpresa (o AdminGlobal) | Actualiza información de la sucursal. |
| DELETE | /api/empresas/{empresaId}/sucursales/{sucursalId} | Policy AdminEmpresa (o AdminGlobal) | Desactiva una sucursal de la empresa. |

### Usuarios de empresa
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/empresas/{empresaId}/usuarios | Policy AdminEmpresa | Crea usuarios (Supervisor/Trabajador/Auditor) dentro de la empresa. |
| GET | /api/empresas/{empresaId}/usuarios | Roles AdminEmpresa, Supervisor, Auditor, AdminGlobal | Lista usuarios con filtros (
ol, habilidad, certificacion, sucursalId, page, pageSize). |
| GET | /api/empresas/{empresaId}/usuarios/{usuarioId} | Roles AdminEmpresa, Supervisor, Auditor, AdminGlobal | Obtiene información de un usuario específico de la empresa. |
| PUT | /api/empresas/{empresaId}/usuarios/{usuarioId} | Policy AdminEmpresa | Actualiza datos del usuario (rol, habilidades, etc.). |
| PUT | /api/empresas/{empresaId}/usuarios/{usuarioId}/password | Autenticado (dueño) u AdminEmpresa/AdminGlobal | Cambia contraseña respetando si la petición viene del propio usuario o un admin. |
| DELETE | /api/empresas/{empresaId}/usuarios/{usuarioId} | Policy AdminEmpresa | Desactiva (soft-delete) un usuario. |

### Tareas
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/tareas | AdminEmpresa, ManagerDepartamento | Crea una tarea en estado Pendiente. ManagerDepartamento solo puede crear tareas para su departamento. |
| GET | /api/tareas | AdminEmpresa, ManagerDepartamento, Usuario | Lista tareas. ManagerDepartamento solo ve tareas de su departamento. Usuario solo ve sus tareas asignadas. |
| GET | /api/tareas/{id} | AdminEmpresa, ManagerDepartamento, Usuario | Obtiene detalle de una tarea. Usuario solo puede ver sus propias tareas. |
| GET | /api/tareas/mis | Usuario | Lista tareas asignadas al usuario autenticado. |
| PUT | /api/tareas/{id}/asignar-manual | AdminEmpresa, ManagerDepartamento | Asigna manualmente una tarea a un worker. ManagerDepartamento solo puede asignar a workers de su departamento. |
| PUT | /api/tareas/{id}/asignar-automatico | AdminEmpresa, ManagerDepartamento | Asignación automática basada en capacidades y disponibilidad. |
| PUT | /api/tareas/{id}/aceptar | Usuario | El worker acepta una tarea asignada. |
| PUT | /api/tareas/{id}/finalizar | Usuario | El worker finaliza una tarea con evidencia. |
| PUT | /api/tareas/{id}/cancelar | AdminEmpresa, ManagerDepartamento | Cancela una tarea pendiente o asignada. |
| PUT | /api/tareas/{id}/reasignar | AdminEmpresa, ManagerDepartamento | Reasigna una tarea a otro worker (manual o automática). |
| **PUT** | **/api/tareas/{id}/delegar** | **AdminEmpresa, ManagerDepartamento** | **Delega una tarea a otro jefe de área. La tarea queda pendiente de aceptación.** |
| **PUT** | **/api/tareas/{id}/aceptar-delegacion** | **ManagerDepartamento** | **El jefe destino acepta la tarea delegada y puede gestionarla.** |
| **PUT** | **/api/tareas/{id}/rechazar-delegacion** | **ManagerDepartamento** | **El jefe destino rechaza la tarea con motivo obligatorio. Regresa al jefe origen.** |

### Asignaciones y flujo de ejecución
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/empresas/{empresaId}/tareas/{tareaId}/asignaciones/manual | Roles Supervisor, AdminEmpresa, AdminGlobal | Asigna manualmente un conjunto de trabajadores a una tarea. |
| POST | /api/empresas/{empresaId}/tareas/{tareaId}/asignaciones/auto | Roles Supervisor, AdminEmpresa, AdminGlobal | Ejecuta asignación automática con un máximo de candidatos (dto.MaxCandidatos). |
| GET | /api/empresas/{empresaId}/tareas/{tareaId}/asignaciones | Roles Supervisor, Auditor, AdminEmpresa, AdminGlobal | Lista asignaciones de una tarea con paginación. |
| GET | /api/empresas/{empresaId}/asignaciones/{asignacionId} | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Obtiene una asignación específica (incluye validaciones de acceso). |
| POST | /api/empresas/{empresaId}/asignaciones/{asignacionId}/acciones/aceptar | Roles Trabajador, AdminGlobal | Trabajador acepta la asignación. |
| POST | .../acciones/declinar | Roles Trabajador, AdminGlobal | Trabajador rechaza la asignación. |
| POST | .../acciones/iniciar | Roles Trabajador, AdminGlobal | Marca inicio de ejecución. |
| POST | .../acciones/pausar | Roles Trabajador, AdminGlobal | Pausa la ejecución indicando motivo opcional. |
| POST | .../acciones/reanudar | Roles Trabajador, AdminGlobal | Reanuda tras una pausa. |
| POST | .../acciones/finalizar | Roles Trabajador, AdminGlobal | Marca finalización y libera datos de tiempo. |
| POST | .../acciones/validar | Roles Supervisor, AdminEmpresa, AdminGlobal | Supervisor valida la asignación y cierra la tarea asociada si corresponde. |
| POST | .../acciones/corregir | Roles Supervisor, AdminEmpresa, AdminGlobal | Devuelve la asignación a estado de corrección para re-trabajo. |

> Todas las acciones POST de asignaciones se benefician del Idempotency-Key para evitar estados duplicados.

### Chats
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| GET | /api/empresas/{empresaId}/tareas/{tareaId}/chat | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Obtiene/crea el hilo de chat vinculado a la tarea y lista mensajes (fterId, pageSize). |
| POST | /api/empresas/{empresaId}/tareas/{tareaId}/chat/mensajes | Roles Supervisor, Trabajador, AdminEmpresa, AdminGlobal | Envía un mensaje al chat de la tarea. |
| GET | /api/empresas/{empresaId}/chat/global | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Obtiene o crea chat global de la empresa (opcionalmente filtrado por sucursalId). |
| POST | /api/empresas/{empresaId}/chat/global/mensajes | Roles Supervisor, Trabajador, AdminEmpresa, AdminGlobal | Publica mensaje en chat global. |
| POST | /api/empresas/{empresaId}/chats/1a1 | Roles Supervisor, Trabajador, AdminEmpresa, AdminGlobal | Crea u obtiene un chat 1 a 1 con UsuarioIdDestino y devuelve su metadata. |
| GET | /api/empresas/{empresaId}/chats/{chatId}/mensajes | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Lista mensajes de un chat específico con paginación incremental. |
| POST | /api/empresas/{empresaId}/chats/{chatId}/mensajes | Roles Supervisor, Trabajador, AdminEmpresa, AdminGlobal | Envía mensaje directo a un chat existente. |
| DELETE | /api/empresas/{empresaId}/chats/{chatId}/mensajes/{mensajeId} | Roles Supervisor, AdminEmpresa, AdminGlobal | Elimina un mensaje (moderación). |
| GET | /api/empresas/{empresaId}/chats/{chatId}/participantes | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Devuelve los usuarios participantes de un chat. |

### Evidencias
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| POST | /api/empresas/{empresaId}/tareas/{tareaId}/evidencias | Roles Trabajador, AdminGlobal | Sube evidencia (multipart, 100 MB) ligada a una tarea y al trabajador asignado. |
| GET | /api/empresas/{empresaId}/tareas/{tareaId}/evidencias | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Lista evidencias de la tarea con paginación. |
| GET | /api/empresas/{empresaId}/evidencias/{evidenciaId} | Roles Supervisor, Trabajador, Auditor, AdminEmpresa, AdminGlobal | Obtiene una evidencia específica de la empresa. |
| DELETE | /api/empresas/{empresaId}/evidencias/{evidenciaId} | Roles Supervisor, AdminEmpresa, AdminGlobal | Elimina (soft-delete) una evidencia. |
| GET | /api/empresas/{empresaId}/tareas/{tareaId}/evidencias/validacion | Roles Supervisor, AdminEmpresa, AdminGlobal | Ejecuta validaciones automáticas sobre las evidencias de la tarea (tipos/cantidad). |

### Auditoría
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| GET | /api/empresas/{empresaId}/auditoria | Roles AdminGlobal, AdminEmpresa, Auditor, Supervisor | Lista eventos de auditoría filtrando por entidad, usuarioId, 	areaId, 	ipo, fechas y paginación. |
| GET | /api/empresas/{empresaId}/auditoria/{eventoId} | Roles AdminGlobal, AdminEmpresa, Auditor, Supervisor | Devuelve detalles puntuales de un evento de auditoría. |
| GET | /api/empresas/{empresaId}/auditoria/export/csv | Roles AdminGlobal, AdminEmpresa, Auditor, Supervisor | Exporta los eventos filtrados a CSV (stream de archivo). |
| GET | /api/empresas/{empresaId}/auditoria/export/pdf | Roles AdminGlobal, AdminEmpresa, Auditor, Supervisor | Exporta los eventos filtrados a PDF usando el helper PdfMinimal. |

### Métricas
| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| GET | /api/empresas/{empresaId}/metricas/resumen | Roles AdminEmpresa, Supervisor, Auditor, AdminGlobal | Calcula KPIs de productividad y cumplimiento en un rango (desde, hasta, sucursalId). |

---

## 3. Sistema de Chat con WebSockets/SignalR

### Endpoints de Chat General

El sistema integra un sistema de chat en tiempo real con soporte para conversaciones 1:1 y grupales utilizando SignalR.

| Método | Ruta | Roles/Política | Descripción |
| --- | --- | --- | --- |
| GET | /api/users/search | Autenticado | Busca usuarios por nombre o email para iniciar chats (query params: `q`, `take`). |
| GET | /api/chats | Autenticado | Lista todos los chats del usuario autenticado con último mensaje y miembros. |
| POST | /api/chats/one-to-one | Autenticado | Crea o recupera un chat 1:1 con otro usuario (body: `{"userId": int}`). |
| POST | /api/chats/group | Autenticado | Crea un chat grupal (body: `{"name": string, "memberIds": int[]}`). |
| POST | /api/chats/{chatId}/members | Autenticado | Agrega un miembro a un chat grupal existente (body: `{"userId": int}`). |
| GET | /api/chats/{chatId}/messages | Autenticado | Obtiene mensajes de un chat con paginación (query params: `skip`, `take`). |
| POST | /api/chats/{chatId}/messages | Autenticado | Envía un mensaje a un chat y notifica en tiempo real vía SignalR (body: `{"text": string}`). |

### Hub SignalR

| Endpoint | Métodos | Descripción |
| --- | --- | --- |
| /apphub | JoinChat(Guid chatId)<br>LeaveChat(Guid chatId) | Hub autorizado para comunicación en tiempo real. Se conecta con `?access_token={jwt}` |

#### Eventos SignalR
- **chat:message**: Evento emitido cuando se envía un mensaje. Payload: `{id, body, createdAt, senderId, chatId}`

#### Conexión al Hub
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/apphub?access_token=" + yourJwtToken)
    .build();

await connection.start();
await connection.invoke("JoinChat", chatId);

connection.on("chat:message", (message) => {
    console.log("Nuevo mensaje:", message);
});
```

