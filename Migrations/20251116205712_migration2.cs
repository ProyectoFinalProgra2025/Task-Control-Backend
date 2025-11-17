using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskControlBackend.Migrations
{
    /// <inheritdoc />
    public partial class migration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tareas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Departamento = table.Column<int>(type: "int", nullable: true),
                    AsignadoAUsuarioId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUsuarioId = table.Column<int>(type: "int", nullable: false),
                    EvidenciaTexto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenciaImagenUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalizadaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tareas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tareas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tareas_Usuarios_AsignadoAUsuarioId",
                        column: x => x.AsignadoAUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tareas_Usuarios_CreatedByUsuarioId",
                        column: x => x.CreatedByUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TareasCapacidadesRequeridas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TareaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasCapacidadesRequeridas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasCapacidadesRequeridas_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_AsignadoAUsuarioId",
                table: "Tareas",
                column: "AsignadoAUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_CreatedByUsuarioId",
                table: "Tareas",
                column: "CreatedByUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_EmpresaId_Estado",
                table: "Tareas",
                columns: new[] { "EmpresaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_TareasCapacidadesRequeridas_TareaId_Nombre",
                table: "TareasCapacidadesRequeridas",
                columns: new[] { "TareaId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TareasCapacidadesRequeridas");

            migrationBuilder.DropTable(
                name: "Tareas");
        }
    }
}
