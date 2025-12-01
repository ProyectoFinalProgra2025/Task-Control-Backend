using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskControlBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBlobStorageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoPerfilUrl",
                table: "Usuarios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TareasDocumentosAdjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TareaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoMime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    SubidoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasDocumentosAdjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasDocumentosAdjuntos_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TareasDocumentosAdjuntos_Usuarios_SubidoPorUsuarioId",
                        column: x => x.SubidoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TareasEvidencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TareaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreArchivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipoMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    SubidoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasEvidencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasEvidencias_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TareasEvidencias_Usuarios_SubidoPorUsuarioId",
                        column: x => x.SubidoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TareaDocumentoAdjunto_TareaId",
                table: "TareasDocumentosAdjuntos",
                column: "TareaId");

            migrationBuilder.CreateIndex(
                name: "IX_TareasDocumentosAdjuntos_SubidoPorUsuarioId",
                table: "TareasDocumentosAdjuntos",
                column: "SubidoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TareaEvidencia_TareaId",
                table: "TareasEvidencias",
                column: "TareaId");

            migrationBuilder.CreateIndex(
                name: "IX_TareasEvidencias_SubidoPorUsuarioId",
                table: "TareasEvidencias",
                column: "SubidoPorUsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TareasDocumentosAdjuntos");

            migrationBuilder.DropTable(
                name: "TareasEvidencias");

            migrationBuilder.DropColumn(
                name: "FotoPerfilUrl",
                table: "Usuarios");
        }
    }
}
