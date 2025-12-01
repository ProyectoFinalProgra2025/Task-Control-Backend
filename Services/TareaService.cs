using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Tarea;
using TaskControlBackend.Hubs;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Services
{
    public class TareaService : ITareaService
            // Guarda evidencia de finalización de tarea
            public async Task<bool> GuardarEvidenciaAsync(Guid tareaId, Guid usuarioId, string texto, List<string> archivoUrls)
            {
                var tarea = await _db.Tareas.FirstOrDefaultAsync(t => t.Id == tareaId && t.IsActive);
                if (tarea == null || tarea.AsignadoAUsuarioId != usuarioId)
                    return false;

                tarea.EvidenciaTexto = texto;
                tarea.EvidenciaArchivoUrls = archivoUrls;
                tarea.FinalizadaAt = DateTime.UtcNow;
                tarea.Estado = EstadoTarea.Finalizada;
                tarea.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                // Emitir evento SignalR si es necesario
                await EmitTareaEventAsync(tarea.EmpresaId, "tarea_finalizada", new { tareaId = tarea.Id });
                return true;
            }
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IHubContext<TareaHub> _hubContext;

        public TareaService(AppDbContext db, IConfiguration config, IHubContext<TareaHub> hubContext)
        {
            _db = db;
            _config = config;
            _hubContext = hubContext;
        }

        private int MaxTareasActivasPorUsuario =>
            int.TryParse(_config["AppSettings:MaxTareasActivasPorUsuario"], out var v) ? v : 5;

        /// <summary>
        /// Emite evento SignalR solo al grupo de empresa (AdminGeneral ya está incluido si está conectado)
        /// </summary>
        private async Task EmitTareaEventAsync(Guid empresaId, string eventName, object payload)
        {
            await _hubContext.Clients.Group($"empresa_{empresaId}").SendAsync(eventName, payload);
        }

        /// <summary>
        /// Registra en el historial una asignación de tarea.
        /// NOTA: Solo agrega al contexto, no guarda. El caller debe hacer SaveChangesAsync.
        /// </summary>
        private void RegistrarAsignacionEnHistorial(
            Guid tareaId,
            Guid? asignadoAUsuarioId,
            Guid? asignadoPorUsuarioId,
            TipoAsignacion tipoAsignacion,
            string? motivo = null)
        {
            var historial = new TareaAsignacionHistorial
            {
                TareaId = tareaId,
                AsignadoAUsuarioId = asignadoAUsuarioId,
                AsignadoPorUsuarioId = asignadoPorUsuarioId,
                TipoAsignacion = tipoAsignacion,
                Motivo = motivo,
                FechaAsignacion = DateTime.UtcNow
            };

            _db.TareasAsignacionesHistorial.Add(historial);
        }

        // ============================================================
        // 5.1  CREAR TAREA (NO ASIGNA)
        // ============================================================
        public async Task<Guid> CreateAsync(Guid empresaId, Guid creadorId, CreateTareaDTO dto)
        {
            // Validar que si el creador es ManagerDepartamento, la tarea debe ser de su departamento
            var creador = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == creadorId && u.EmpresaId == empresaId);
            if (creador == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            if (creador.Rol == RolUsuario.ManagerDepartamento)
            {
                if (!creador.Departamento.HasValue)
                    throw new InvalidOperationException("El jefe de área debe tener un departamento asignado");

                if (!dto.Departamento.HasValue || dto.Departamento.Value != creador.Departamento.Value)
                    throw new InvalidOperationException("Solo puedes crear tareas para tu propio departamento");
            }

            var tarea = new Tarea
            {
                EmpresaId = empresaId,
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                Prioridad = dto.Prioridad,
                DueDate = dto.DueDate,
                Departamento = dto.Departamento,
                CreatedByUsuarioId = creadorId,
                Estado = EstadoTarea.Pendiente
            };

            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                tarea.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            _db.Tareas.Add(tarea);
            await _db.SaveChangesAsync();

            // Emit SignalR event
            await EmitTareaEventAsync(empresaId, "tarea:created", new
            {
                id = tarea.Id,
                titulo = tarea.Titulo,
                empresaId = tarea.EmpresaId,
                estado = tarea.Estado.ToString(),
                prioridad = tarea.Prioridad.ToString(),
                departamento = tarea.Departamento?.ToString(),
                createdAt = tarea.CreatedAt
            });

            return tarea.Id;
        }

        // ============================================================
        // 5.2  ASIGNACIÓN MANUAL (con validación de departamento para jefes)
        // ============================================================
        public async Task AsignarManualAsync(Guid empresaId, Guid tareaId, Guid assignedByUserId, AsignarManualTareaDTO dto)
        {
            var tarea = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (tarea.Estado == EstadoTarea.Finalizada || tarea.Estado == EstadoTarea.Cancelada)
                throw new InvalidOperationException("No se pueden asignar tareas finalizadas o canceladas");

            Usuario? usuario = null;

            // Buscar por ID
            if (dto.UsuarioId.HasValue)
            {
                usuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
                    u.Id == dto.UsuarioId.Value &&
                    u.EmpresaId == empresaId &&
                    (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento) &&
                    u.IsActive);
            }
            // Buscar por nombre
            else if (!string.IsNullOrWhiteSpace(dto.NombreUsuario))
            {
                var nombreBuscado = dto.NombreUsuario.Trim().ToLower();

                var candidatos = await _db.Usuarios
                    .Where(u => u.EmpresaId == empresaId &&
                                (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento) &&
                                u.IsActive &&
                                u.NombreCompleto.ToLower().Contains(nombreBuscado))
                    .ToListAsync();

                if (!candidatos.Any())
                    throw new KeyNotFoundException("No se encontró ningún usuario con ese nombre");

                if (candidatos.Count > 1)
                    throw new InvalidOperationException("Existe más de un usuario con ese nombre; use UsuarioId.");

                usuario = candidatos.Single();
            }
            else
            {
                throw new ArgumentException("Debe enviarse UsuarioId o NombreUsuario");
            }

            if (usuario is null)
                throw new KeyNotFoundException("Usuario no válido para asignación");

            // Validaciones de departamento y skills
            if (!dto.IgnorarValidacionesSkills)
            {
                if (tarea.Departamento.HasValue &&
                    usuario.Departamento.HasValue &&
                    tarea.Departamento.Value != usuario.Departamento.Value)
                {
                    throw new InvalidOperationException("El usuario no pertenece al departamento requerido por la tarea");
                }

                var reqCaps = tarea.CapacidadesRequeridas
                    .Select(c => c.Nombre.Trim().ToLower())
                    .ToList();

                if (reqCaps.Any())
                {
                    var capsUsuario = await _db.UsuarioCapacidades
                        .Where(uc => uc.UsuarioId == usuario.Id)
                        .Include(uc => uc.Capacidad)
                        .Select(uc => uc.Capacidad.Nombre.Trim().ToLower())
                        .ToListAsync();

                    var cumple = reqCaps.All(rc => capsUsuario.Contains(rc));
                    if (!cumple)
                        throw new InvalidOperationException("El usuario no cumple todas las capacidades requeridas");
                }
            }

            // Límite de tareas activas
            var activas = await _db.Tareas.CountAsync(t =>
                t.EmpresaId == empresaId &&
                t.AsignadoAUsuarioId == usuario.Id &&
                (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada));

            if (activas >= MaxTareasActivasPorUsuario)
                throw new InvalidOperationException(
                    $"El trabajador '{usuario.NombreCompleto}' ya tiene el máximo de tareas activas ({MaxTareasActivasPorUsuario}). " +
                    "Por favor, espere a que complete tareas o asigne a otro trabajador.");

            tarea.AsignadoAUsuarioId = usuario.Id;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;

            // Registrar en historial (asignación manual con auditoría completa)
            RegistrarAsignacionEnHistorial(
                tareaId,
                usuario.Id,
                assignedByUserId,
                TipoAsignacion.Manual
            );

            await _db.SaveChangesAsync();

            // Emit SignalR event for task assignment
            await EmitTareaEventAsync(empresaId, "tarea:assigned", new
            {
                id = tarea.Id,
                titulo = tarea.Titulo,
                empresaId = tarea.EmpresaId,
                estado = tarea.Estado.ToString(),
                asignadoAUsuarioId = tarea.AsignadoAUsuarioId,
                asignadoANombre = usuario.NombreCompleto,
                updatedAt = tarea.UpdatedAt
            });
        }

        // ============================================================
        // 5.3  ASIGNACIÓN AUTOMÁTICA PÚBLICA
        // ============================================================
        public async Task AsignarAutomaticamenteAsync(Guid empresaId, Guid tareaId, bool forzarReasignacion)
        {
            var tarea = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (!forzarReasignacion && tarea.AsignadoAUsuarioId != null &&
                (tarea.Estado == EstadoTarea.Asignada || tarea.Estado == EstadoTarea.Aceptada))
            {
                throw new InvalidOperationException("La tarea ya tiene un usuario asignado");
            }

            if (forzarReasignacion)
            {
                tarea.AsignadoAUsuarioId = null;
                tarea.Estado = EstadoTarea.Pendiente;
            }

            await AsignarAutomaticamenteInternoAsync(tarea);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Algoritmo mejorado de asignación automática con sistema de fallback en 3 niveles:
        /// NIVEL 1: Workers con TODAS las capacidades requeridas (match perfecto)
        /// NIVEL 2: Workers con ALGUNAS capacidades requeridas (match parcial)
        /// NIVEL 3: CUALQUIER worker disponible del departamento
        /// 
        /// Garantiza asignación si existe al menos un trabajador disponible en el departamento.
        /// </summary>
        private async Task AsignarAutomaticamenteInternoAsync(Tarea tarea)
        {
            if (tarea.Departamento == null)
                return;

            var reqCaps = tarea.CapacidadesRequeridas
                .Select(cr => cr.Nombre.Trim().ToLower())
                .ToList();

            // Obtener TODOS los candidatos del departamento correcto (sin filtrar por skills aún)
            var candidatos = await _db.Usuarios
                .Include(u => u.UsuarioCapacidades).ThenInclude(uc => uc.Capacidad)
                .Where(u => u.EmpresaId == tarea.EmpresaId &&
                            (u.Rol == RolUsuario.Usuario || u.Rol == RolUsuario.ManagerDepartamento) &&
                            u.IsActive &&
                            u.Departamento == tarea.Departamento)
                .ToListAsync();

            if (!candidatos.Any())
                return; // No hay trabajadores en el departamento

            // OPTIMIZACIÓN: Cargar la carga de trabajo de todos los candidatos en una sola query
            var candidatoIds = candidatos.Select(c => c.Id).ToList();
            var cargasPorUsuario = await _db.Tareas
                .Where(t => t.EmpresaId == tarea.EmpresaId &&
                            t.AsignadoAUsuarioId.HasValue &&
                            candidatoIds.Contains(t.AsignadoAUsuarioId.Value) &&
                            (t.Estado == EstadoTarea.Asignada || t.Estado == EstadoTarea.Aceptada))
                .GroupBy(t => t.AsignadoAUsuarioId)
                .Select(g => new { UsuarioId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.UsuarioId, x => x.Count);

            // Estructuras para los 3 niveles de candidatos
            var nivel1_MatchPerfecto = new List<(Usuario usuario, int carga, int score)>();
            var nivel2_MatchParcial = new List<(Usuario usuario, int carga, int score, int skillsMatch)>();
            var nivel3_Cualquiera = new List<(Usuario usuario, int carga, int score)>();

            // Clasificar candidatos por nivel
            foreach (var u in candidatos)
            {
                var activas = cargasPorUsuario.GetValueOrDefault(u.Id, 0);

                // Skip si está sobrecargado (ya tiene 5+ tareas)
                if (activas >= MaxTareasActivasPorUsuario)
                    continue;

                var capsUsuario = u.UsuarioCapacidades
                    .Select(uc => uc.Capacidad.Nombre.Trim().ToLower())
                    .ToHashSet();

                int score = 100;

                // Penalizar por carga de trabajo (menos tareas = mejor)
                score -= activas * 15;

                // Bonus por nivel de habilidad
                if (u.NivelHabilidad.HasValue)
                    score += u.NivelHabilidad.Value * 5;

                // Bonus por prioridad de tarea (tareas urgentes a workers menos ocupados)
                if (tarea.Prioridad == PrioridadTarea.High && activas == 0)
                    score += 50;

                // Determinar nivel según match de capacidades
                if (reqCaps.Any())
                {
                    var skillsMatchCount = reqCaps.Count(rc => capsUsuario.Contains(rc));
                    var skillsMatchPercentage = (double)skillsMatchCount / reqCaps.Count;

                    if (skillsMatchCount == reqCaps.Count)
                    {
                        // NIVEL 1: Match perfecto (tiene todas las capacidades)
                        score += 50; // Bonus por match perfecto
                        nivel1_MatchPerfecto.Add((u, activas, score));
                    }
                    else if (skillsMatchCount > 0)
                    {
                        // NIVEL 2: Match parcial (tiene algunas capacidades)
                        score += (int)(skillsMatchPercentage * 40); // Bonus proporcional al % de match
                        nivel2_MatchParcial.Add((u, activas, score, skillsMatchCount));
                    }
                    else
                    {
                        // NIVEL 3: Sin capacidades requeridas, pero disponible
                        nivel3_Cualquiera.Add((u, activas, score));
                    }
                }
                else
                {
                    // Si la tarea no requiere capacidades específicas, todos son nivel 1
                    nivel1_MatchPerfecto.Add((u, activas, score));
                }
            }

            // Intentar asignación por niveles (primero nivel 1, luego 2, finalmente 3)
            Usuario? elegido = null;

            if (nivel1_MatchPerfecto.Any())
            {
                // NIVEL 1: Priorizar match perfecto
                elegido = nivel1_MatchPerfecto
                    .OrderByDescending(c => c.score)
                    .ThenBy(c => c.carga)
                    .ThenBy(c => c.usuario.Id)
                    .First().usuario;
            }
            else if (nivel2_MatchParcial.Any())
            {
                // NIVEL 2: Match parcial (ordenar por mayor cantidad de skills matched)
                elegido = nivel2_MatchParcial
                    .OrderByDescending(c => c.skillsMatch) // Primero por cantidad de skills
                    .ThenByDescending(c => c.score)        // Luego por score
                    .ThenBy(c => c.carga)                   // Finalmente por carga
                    .ThenBy(c => c.usuario.Id)
                    .First().usuario;
            }
            else if (nivel3_Cualquiera.Any())
            {
                // NIVEL 3: Cualquier trabajador disponible del departamento
                elegido = nivel3_Cualquiera
                    .OrderByDescending(c => c.score)
                    .ThenBy(c => c.carga)
                    .ThenBy(c => c.usuario.Id)
                    .First().usuario;
            }

            // Si después de los 3 niveles no hay elegido, es porque todos están sobrecargados
            if (elegido == null)
            {
                var departamentoNombre = tarea.Departamento?.ToString() ?? "desconocido";
                throw new InvalidOperationException(
                    $"No se pudo asignar la tarea. Todos los trabajadores del departamento '{departamentoNombre}' " +
                    $"tienen el máximo de tareas activas ({MaxTareasActivasPorUsuario}). " +
                    "Por favor, reasigne manualmente o espere a que se completen tareas.");
            }

            // Asignar la tarea
            tarea.AsignadoAUsuarioId = elegido.Id;
            tarea.Estado = EstadoTarea.Asignada;
            tarea.UpdatedAt = DateTime.UtcNow;

            // Determinar nivel usado para el historial
            string nivelAsignacion = nivel1_MatchPerfecto.Any(c => c.usuario.Id == elegido.Id)
                ? "Match Perfecto"
                : nivel2_MatchParcial.Any(c => c.usuario.Id == elegido.Id)
                    ? $"Match Parcial ({nivel2_MatchParcial.First(c => c.usuario.Id == elegido.Id).skillsMatch}/{reqCaps.Count} skills)"
                    : "Asignación Básica (sin match de capacidades)";

            // Registrar en historial (asignación automática, no hay quien asigna)
            RegistrarAsignacionEnHistorial(
                tarea.Id,
                elegido.Id,
                null, // Sistema automático
                TipoAsignacion.Automatica,
                nivelAsignacion
            );
        }

        // ============================================================
        // LISTAR TAREAS (con filtrado por departamento para jefes)
        // ============================================================
        public async Task<List<TareaListDTO>> ListAsync(
            Guid empresaId, RolUsuario rol, Guid userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento, Guid? asignadoAUsuarioId)
        {
            var q = _db.Tareas
                .Include(t => t.AsignadoAUsuario)
                .Include(t => t.CreatedByUsuario)
                .AsNoTracking()
                .Where(t => t.EmpresaId == empresaId && t.IsActive);

            // Usuario (Worker) solo ve sus tareas asignadas
            if (rol == RolUsuario.Usuario)
            {
                q = q.Where(t => t.AsignadoAUsuarioId == userId);
            }
            // ManagerDepartamento ve tareas de SU DEPARTAMENTO (no solo las asignadas a él)
            else if (rol == RolUsuario.ManagerDepartamento)
            {
                // Obtener el departamento del manager
                var manager = await _db.Usuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId && u.EmpresaId == empresaId);
                
                if (manager?.Departamento.HasValue == true)
                {
                    // Ver todas las tareas de su departamento
                    q = q.Where(t => t.Departamento == manager.Departamento.Value);
                }
                else
                {
                    // Si no tiene departamento asignado, no ve nada
                    q = q.Where(t => false);
                }
            }
            else if (asignadoAUsuarioId.HasValue)
            {
                q = q.Where(t => t.AsignadoAUsuarioId == asignadoAUsuarioId.Value);
            }

            if (estado.HasValue) q = q.Where(t => t.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(t => t.Prioridad == prioridad.Value);
            if (departamento.HasValue) q = q.Where(t => t.Departamento == departamento.Value);

            return await q
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TareaListDTO
                {
                    Id = t.Id,
                    Titulo = t.Titulo,
                    Descripcion = t.Descripcion,
                    Estado = t.Estado,
                    Prioridad = t.Prioridad,
                    DueDate = t.DueDate,
                    Departamento = t.Departamento,
                    AsignadoAUsuarioId = t.AsignadoAUsuarioId,
                    AsignadoAUsuarioNombre = t.AsignadoAUsuario != null ? t.AsignadoAUsuario.NombreCompleto : null,
                    CreatedByUsuarioId = t.CreatedByUsuarioId,
                    CreatedByUsuarioNombre = t.CreatedByUsuario != null ? t.CreatedByUsuario.NombreCompleto : string.Empty,
                    EstaDelegada = t.EstaDelegada,
                    CreatedAt = t.CreatedAt
                }).ToListAsync();
        }

        // ============================================================
        // LISTAR MIS TAREAS (solo las asignadas al usuario)
        // Para Workers y Managers en vista "Mis Tareas"
        // ============================================================
        public async Task<List<TareaListDTO>> ListMisTareasAsync(
            Guid empresaId, Guid userId,
            EstadoTarea? estado, PrioridadTarea? prioridad, Departamento? departamento)
        {
            var q = _db.Tareas
                .Include(t => t.AsignadoAUsuario)
                .Include(t => t.CreatedByUsuario)
                .AsNoTracking()
                .Where(t => t.EmpresaId == empresaId && t.IsActive)
                .Where(t => t.AsignadoAUsuarioId == userId); // Solo tareas asignadas a este usuario

            if (estado.HasValue) q = q.Where(t => t.Estado == estado.Value);
            if (prioridad.HasValue) q = q.Where(t => t.Prioridad == prioridad.Value);
            if (departamento.HasValue) q = q.Where(t => t.Departamento == departamento.Value);

            return await q
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TareaListDTO
                {
                    Id = t.Id,
                    Titulo = t.Titulo,
                    Descripcion = t.Descripcion,
                    Estado = t.Estado,
                    Prioridad = t.Prioridad,
                    DueDate = t.DueDate,
                    Departamento = t.Departamento,
                    AsignadoAUsuarioId = t.AsignadoAUsuarioId,
                    AsignadoAUsuarioNombre = t.AsignadoAUsuario != null ? t.AsignadoAUsuario.NombreCompleto : null,
                    CreatedByUsuarioId = t.CreatedByUsuarioId,
                    CreatedByUsuarioNombre = t.CreatedByUsuario != null ? t.CreatedByUsuario.NombreCompleto : string.Empty,
                    EstaDelegada = t.EstaDelegada,
                    CreatedAt = t.CreatedAt
                }).ToListAsync();
        }

        // ============================================================
        // OBTENER DETALLE
        // ============================================================
        public async Task<TareaDetalleDTO?> GetAsync(Guid empresaId, RolUsuario rol, Guid userId, Guid tareaId)
        {
            var t = await _db.Tareas
                .Include(t => t.AsignadoAUsuario)
                .Include(t => t.CreatedByUsuario)
                .Include(t => t.CapacidadesRequeridas)
                .Include(t => t.DelegadoPorUsuario)
                .Include(t => t.DelegadoAUsuario)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) return null;

            if (rol == RolUsuario.Usuario && t.AsignadoAUsuarioId != userId)
                throw new UnauthorizedAccessException("No autorizado a ver esta tarea");

            return new TareaDetalleDTO
            {
                Id = t.Id,
                EmpresaId = t.EmpresaId,
                Titulo = t.Titulo,
                Descripcion = t.Descripcion,
                Estado = t.Estado,
                Prioridad = t.Prioridad,
                DueDate = t.DueDate,
                Departamento = t.Departamento,
                CapacidadesRequeridas = t.CapacidadesRequeridas.Select(cr => cr.Nombre).ToList(),
                AsignadoAUsuarioId = t.AsignadoAUsuarioId,
                AsignadoAUsuarioNombre = t.AsignadoAUsuario != null ? t.AsignadoAUsuario.NombreCompleto : null,
                CreatedByUsuarioId = t.CreatedByUsuarioId,
                CreatedByUsuarioNombre = t.CreatedByUsuario != null ? t.CreatedByUsuario.NombreCompleto : string.Empty,
                EvidenciaTexto = t.EvidenciaTexto,
                EvidenciaImagenUrl = t.EvidenciaImagenUrl,
                EstaDelegada = t.EstaDelegada,
                DelegadoPorUsuarioId = t.DelegadoPorUsuarioId,
                DelegadoPorUsuarioNombre = t.DelegadoPorUsuario != null ? t.DelegadoPorUsuario.NombreCompleto : null,
                DelegadoAUsuarioId = t.DelegadoAUsuarioId,
                DelegadoAUsuarioNombre = t.DelegadoAUsuario != null ? t.DelegadoAUsuario.NombreCompleto : null,
                DelegadaAt = t.DelegadaAt,
                DelegacionAceptada = t.DelegacionAceptada,
                MotivoRechazoJefe = t.MotivoRechazoJefe,
                DelegacionResueltaAt = t.DelegacionResueltaAt,
                CreatedAt = t.CreatedAt,
                FinalizadaAt = t.FinalizadaAt,
                MotivoCancelacion = t.MotivoCancelacion
            };
        }

        // ============================================================
        // ACTUALIZAR TAREA
        // ============================================================
        public async Task UpdateAsync(Guid empresaId, Guid tareaId, UpdateTareaDTO dto)
        {
            var t = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado != EstadoTarea.Pendiente)
                throw new InvalidOperationException("Solo se pueden editar tareas en estado Pendiente");

            t.Titulo = dto.Titulo;
            t.Descripcion = dto.Descripcion;
            t.Prioridad = dto.Prioridad;
            t.DueDate = dto.DueDate;
            t.Departamento = dto.Departamento;

            t.CapacidadesRequeridas.Clear();
            foreach (var nombre in dto.CapacidadesRequeridas.Select(n => n.Trim()))
            {
                t.CapacidadesRequeridas.Add(new TareaCapacidadRequerida { Nombre = nombre });
            }

            t.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Emit SignalR event for task update
            await EmitTareaEventAsync(empresaId, "tarea:updated", new
            {
                id = t.Id,
                titulo = t.Titulo,
                descripcion = t.Descripcion,
                empresaId,
                estado = t.Estado.ToString(),
                prioridad = t.Prioridad.ToString(),
                departamento = t.Departamento?.ToString(),
                dueDate = t.DueDate,
                updatedAt = t.UpdatedAt
            });
        }

        // ============================================================
        // ACEPTAR
        // ============================================================
        public async Task AceptarAsync(Guid empresaId, Guid tareaId, Guid usuarioId)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");
            
            // Validar que el usuario sea el asignado O el delegado
            if (t.AsignadoAUsuarioId != usuarioId && t.DelegadoAUsuarioId != usuarioId)
                throw new UnauthorizedAccessException("No eres el usuario asignado a esta tarea");
            
            if (t.Estado != EstadoTarea.Asignada)
                throw new InvalidOperationException("La tarea debe estar en estado Asignada");

            t.Estado = EstadoTarea.Aceptada;
            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Emit SignalR event for task acceptance
            await EmitTareaEventAsync(empresaId, "tarea:accepted", new
            {
                id = t.Id,
                titulo = t.Titulo,
                empresaId,
                estado = t.Estado.ToString(),
                asignadoAUsuarioId = t.AsignadoAUsuarioId,
                updatedAt = t.UpdatedAt
            });
        }

        // ============================================================
        // FINALIZAR
        // ============================================================
        public async Task FinalizarAsync(Guid empresaId, Guid tareaId, Guid usuarioId, FinalizarTareaDTO dto)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");
            if (t.AsignadoAUsuarioId != usuarioId)
                throw new UnauthorizedAccessException("No eres el usuario asignado a esta tarea");
            if (t.Estado != EstadoTarea.Aceptada)
                throw new InvalidOperationException("Solo se pueden finalizar tareas en estado Aceptada");

            t.Estado = EstadoTarea.Finalizada;
            t.EvidenciaTexto = dto.EvidenciaTexto;
            t.EvidenciaImagenUrl = dto.EvidenciaImagenUrl;
            t.FinalizadaAt = DateTime.UtcNow;
            t.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Emit SignalR event for task completion
            await EmitTareaEventAsync(empresaId, "tarea:completed", new
            {
                id = t.Id,
                titulo = t.Titulo,
                empresaId,
                estado = t.Estado.ToString(),
                asignadoAUsuarioId = t.AsignadoAUsuarioId,
                finalizadaAt = t.FinalizadaAt,
                updatedAt = t.UpdatedAt
            });
        }

        // ============================================================
        // CANCELAR / RECHAZAR
        // ============================================================
        public async Task CancelarAsync(Guid empresaId, Guid tareaId, Guid usuarioId, string? motivo)
        {
            var t = await _db.Tareas.FirstOrDefaultAsync(t =>
                t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado != EstadoTarea.Pendiente && t.Estado != EstadoTarea.Asignada)
                throw new InvalidOperationException("Solo se pueden cancelar tareas Pendientes o Asignadas");

            // Si es un manager rechazando (no es admin ni creador), marcar como rechazo de delegación
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
            if (usuario?.Rol == RolUsuario.ManagerDepartamento && t.DelegadoAUsuarioId == usuarioId)
            {
                t.DelegacionAceptada = false;
                t.MotivoRechazoJefe = motivo;
                t.DelegacionResueltaAt = DateTime.UtcNow;
                t.AsignadoAUsuarioId = null; // Liberar asignación
                t.Estado = EstadoTarea.Pendiente; // Volver a pendiente
            }
            else
            {
                t.Estado = EstadoTarea.Cancelada;
                t.MotivoCancelacion = motivo;
            }
            
            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Emit SignalR event for task cancellation
            await EmitTareaEventAsync(empresaId, "tarea:cancelled", new
            {
                id = t.Id,
                titulo = t.Titulo,
                empresaId,
                estado = t.Estado.ToString(),
                motivoCancelacion = t.MotivoCancelacion,
                updatedAt = t.UpdatedAt
            });
        }

        // ============================================================
        // REASIGNAR (diferente de asignación inicial)
        // ============================================================
        public async Task ReasignarAsync(Guid empresaId, Guid tareaId, Guid adminEmpresaId, Guid? nuevoUsuarioId, bool asignacionAutomatica, string? motivo = null)
        {
            var t = await _db.Tareas
                .Include(t => t.CapacidadesRequeridas)
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (t is null) throw new KeyNotFoundException("Tarea no encontrada");

            if (t.Estado == EstadoTarea.Finalizada || t.Estado == EstadoTarea.Cancelada)
                throw new InvalidOperationException("No se pueden reasignar tareas finalizadas o canceladas");

            // Guardar usuario anterior
            var usuarioAnteriorId = t.AsignadoAUsuarioId;

            // Limpiar asignación anterior
            t.AsignadoAUsuarioId = null;
            t.Estado = EstadoTarea.Pendiente;

            if (nuevoUsuarioId.HasValue)
            {
                // Asignar manualmente al nuevo usuario
                var nuevoUsuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
                    u.Id == nuevoUsuarioId.Value &&
                    u.EmpresaId == empresaId &&
                    u.IsActive);

                if (nuevoUsuario is null)
                    throw new KeyNotFoundException("Usuario destino no encontrado");

                t.AsignadoAUsuarioId = nuevoUsuario.Id;
                t.Estado = EstadoTarea.Asignada;

                // Registrar reasignación en historial
                RegistrarAsignacionEnHistorial(
                    tareaId,
                    nuevoUsuario.Id,
                    adminEmpresaId,
                    TipoAsignacion.Reasignacion,
                    motivo ?? $"Reasignado de {usuarioAnteriorId} a {nuevoUsuario.Id}"
                );
            }
            else if (asignacionAutomatica)
            {
                await AsignarAutomaticamenteInternoAsync(t);

                // Registrar reasignación automática
                if (t.AsignadoAUsuarioId.HasValue)
                {
                    RegistrarAsignacionEnHistorial(
                        tareaId,
                        t.AsignadoAUsuarioId,
                        adminEmpresaId,
                        TipoAsignacion.Reasignacion,
                        motivo ?? "Reasignación automática"
                    );
                }
            }

            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Emitir evento de reasignación
            if (t.AsignadoAUsuarioId.HasValue)
            {
                await EmitTareaEventAsync(empresaId, "tarea:reasignada", new
                {
                    id = t.Id,
                    titulo = t.Titulo,
                    empresaId,
                    usuarioAnteriorId,
                    nuevoUsuarioId = t.AsignadoAUsuarioId,
                    motivo,
                    updatedAt = t.UpdatedAt
                });
            }
        }

        // ============================================================
        // HISTORIAL DE ASIGNACIONES
        // ============================================================
        public async Task<List<TareaAsignacionHistorialDTO>> GetHistorialAsignacionesAsync(Guid tareaId)
        {
            return await _db.TareasAsignacionesHistorial
                .Include(h => h.AsignadoAUsuario)
                .Include(h => h.AsignadoPorUsuario)
                .Where(h => h.TareaId == tareaId)
                .OrderByDescending(h => h.FechaAsignacion)
                .Select(h => new TareaAsignacionHistorialDTO
                {
                    Id = h.Id,
                    AsignadoAUsuarioId = h.AsignadoAUsuarioId,
                    AsignadoAUsuarioNombre = h.AsignadoAUsuario != null ? h.AsignadoAUsuario.NombreCompleto : null,
                    AsignadoPorUsuarioId = h.AsignadoPorUsuarioId,
                    AsignadoPorUsuarioNombre = h.AsignadoPorUsuario != null ? h.AsignadoPorUsuario.NombreCompleto : "Sistema",
                    TipoAsignacion = h.TipoAsignacion.ToString(),
                    Motivo = h.Motivo,
                    FechaAsignacion = h.FechaAsignacion
                })
                .ToListAsync();
        }

        // ============================================================
        // DELEGACIÓN ENTRE JEFES DE ÁREA
        // ============================================================

        /// <summary>
        /// Un jefe delega una tarea a otro jefe de un departamento diferente.
        /// La tarea queda en estado de espera hasta que el jefe destino la acepte o rechace.
        /// </summary>
        public async Task DelegarTareaAJefeAsync(Guid empresaId, Guid tareaId, Guid jefeOrigenId, DelegarTareaDTO dto)
        {
            // Validar tarea
            var tarea = await _db.Tareas
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (tarea.Estado == EstadoTarea.Finalizada || tarea.Estado == EstadoTarea.Cancelada)
                throw new InvalidOperationException("No se pueden delegar tareas finalizadas o canceladas");

            // Validar jefe origen
            var jefeOrigen = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == jefeOrigenId && u.EmpresaId == empresaId && u.IsActive);

            if (jefeOrigen == null)
                throw new UnauthorizedAccessException("Jefe origen no encontrado");

            if (jefeOrigen.Rol != RolUsuario.ManagerDepartamento && jefeOrigen.Rol != RolUsuario.AdminEmpresa)
                throw new InvalidOperationException("Solo jefes de área o admin de empresa pueden delegar tareas");

            // Si es ManagerDepartamento, validar que la tarea sea de su departamento
            if (jefeOrigen.Rol == RolUsuario.ManagerDepartamento)
            {
                if (!jefeOrigen.Departamento.HasValue)
                    throw new InvalidOperationException("El jefe debe tener un departamento asignado");

                if (tarea.Departamento != jefeOrigen.Departamento.Value && tarea.CreatedByUsuarioId != jefeOrigenId)
                    throw new InvalidOperationException("Solo puedes delegar tareas de tu departamento o creadas por ti");
            }

            // Validar jefe destino
            var jefeDestino = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Id == dto.JefeDestinoId && u.EmpresaId == empresaId && u.IsActive);

            if (jefeDestino == null)
                throw new KeyNotFoundException("Jefe destino no encontrado");

            if (jefeDestino.Rol != RolUsuario.ManagerDepartamento)
                throw new InvalidOperationException("Solo se puede delegar a jefes de área");

            if (!jefeDestino.Departamento.HasValue)
                throw new InvalidOperationException("El jefe destino debe tener un departamento asignado");

            if (jefeOrigen.Id == jefeDestino.Id)
                throw new InvalidOperationException("No puedes delegar una tarea a ti mismo");

            // Actualizar tarea con delegación
            tarea.EstaDelegada = true;
            tarea.DelegadoPorUsuarioId = jefeOrigenId;
            tarea.DelegadoAUsuarioId = dto.JefeDestinoId;
            tarea.DelegadaAt = DateTime.UtcNow;
            tarea.DelegacionAceptada = null; // Pendiente
            tarea.UpdatedAt = DateTime.UtcNow;

            // Opcional: cambiar departamento de la tarea al del jefe destino
            tarea.Departamento = jefeDestino.Departamento;

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// El jefe destino acepta la tarea delegada y puede proceder a asignarla o trabajarla.
        /// </summary>
        public async Task AceptarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, AceptarDelegacionDTO dto)
        {
            var tarea = await _db.Tareas
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (!tarea.EstaDelegada)
                throw new InvalidOperationException("Esta tarea no está delegada");

            if (tarea.DelegadoAUsuarioId != jefeDestinoId)
                throw new UnauthorizedAccessException("No eres el jefe al que se delegó esta tarea");

            if (tarea.DelegacionAceptada.HasValue)
                throw new InvalidOperationException("Esta delegación ya fue resuelta");

            // Aceptar delegación
            tarea.DelegacionAceptada = true;
            tarea.DelegacionResueltaAt = DateTime.UtcNow;
            tarea.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// El jefe destino rechaza la tarea delegada con un motivo obligatorio.
        /// La tarea regresa al jefe origen.
        /// </summary>
        public async Task RechazarDelegacionAsync(Guid empresaId, Guid tareaId, Guid jefeDestinoId, RechazarDelegacionDTO dto)
        {
            var tarea = await _db.Tareas
                .FirstOrDefaultAsync(t => t.Id == tareaId && t.EmpresaId == empresaId && t.IsActive);

            if (tarea is null)
                throw new KeyNotFoundException("Tarea no encontrada");

            if (!tarea.EstaDelegada)
                throw new InvalidOperationException("Esta tarea no está delegada");

            if (tarea.DelegadoAUsuarioId != jefeDestinoId)
                throw new UnauthorizedAccessException("No eres el jefe al que se delegó esta tarea");

            if (tarea.DelegacionAceptada.HasValue)
                throw new InvalidOperationException("Esta delegación ya fue resuelta");

            // Obtener jefe origen para devolver el departamento
            var jefeOrigen = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == tarea.DelegadoPorUsuarioId);

            // Rechazar delegación
            tarea.DelegacionAceptada = false;
            tarea.MotivoRechazoJefe = dto.MotivoRechazo;
            tarea.DelegacionResueltaAt = DateTime.UtcNow;
            tarea.UpdatedAt = DateTime.UtcNow;

            // Revertir al departamento original si es posible
            if (jefeOrigen?.Departamento.HasValue == true)
            {
                tarea.Departamento = jefeOrigen.Departamento;
            }

            await _db.SaveChangesAsync();
        }
    }
}
