using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordin.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandDependencies",
                columns: table => new
                {
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrerequisiteDemandId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandDependencies", x => new { x.DemandId, x.PrerequisiteDemandId });
                    table.CheckConstraint("CK_DemandDependencies_NoSelfDependency", "\"DemandId\" <> \"PrerequisiteDemandId\"");
                    table.ForeignKey(
                        name: "FK_DemandDependencies_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandDependencies_Demands_PrerequisiteDemandId",
                        column: x => x.PrerequisiteDemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandDependencies_PrerequisiteDemandId",
                table: "DemandDependencies",
                column: "PrerequisiteDemandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandDependencies");
        }
    }
}
