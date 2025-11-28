using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskControlBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTareaAsignacionHistorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TareasAsignacionesHistorial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TareaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignadoAUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AsignadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoAsignacion = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasAsignacionesHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasAsignacionesHistorial_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TareasAsignacionesHistorial_Usuarios_AsignadoAUsuarioId",
                        column: x => x.AsignadoAUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TareasAsignacionesHistorial_Usuarios_AsignadoPorUsuarioId",
                        column: x => x.AsignadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TareasAsignacionesHistorial_AsignadoAUsuarioId",
                table: "TareasAsignacionesHistorial",
                column: "AsignadoAUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TareasAsignacionesHistorial_AsignadoPorUsuarioId",
                table: "TareasAsignacionesHistorial",
                column: "AsignadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TareasAsignacionesHistorial_TareaId",
                table: "TareasAsignacionesHistorial",
                column: "TareaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TareasAsignacionesHistorial");
        }
    }
}
