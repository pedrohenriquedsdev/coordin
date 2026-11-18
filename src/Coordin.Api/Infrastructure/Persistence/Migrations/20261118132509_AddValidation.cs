using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordin.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValidationBaselines",
                columns: table => new
                {
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationBaselines", x => x.DemandId);
                    table.ForeignKey(
                        name: "FK_ValidationBaselines_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationCurrentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationCurrentPlans", x => x.Id);
                    table.CheckConstraint("CK_ValidationCurrentPlans_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValidationBaselines");

            migrationBuilder.DropTable(
                name: "ValidationCurrentPlans");
        }
    }
}
