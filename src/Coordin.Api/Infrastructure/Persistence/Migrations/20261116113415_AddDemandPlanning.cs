using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordin.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Demands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PriorityRank = table.Column<int>(type: "integer", nullable: false),
                    ExecutionRank = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InformedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demands", x => x.Id);
                    table.CheckConstraint("CK_Demands_CompletedAfterStart", "\"CompletedOn\" IS NULL OR \"StartDate\" IS NULL OR \"CompletedOn\" >= \"StartDate\"");
                    table.CheckConstraint("CK_Demands_ExecutionRank", "\"ExecutionRank\" >= 1");
                    table.CheckConstraint("CK_Demands_PriorityRank", "\"PriorityRank\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "PlanningSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ExecutionOrderMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SafetyMarginPercent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningSettings", x => x.Id);
                    table.CheckConstraint("CK_PlanningSettings_SafetyMargin", "\"SafetyMarginPercent\" >= 0");
                    table.CheckConstraint("CK_PlanningSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "DemandAssignments",
                columns: table => new
                {
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandAssignments", x => new { x.DemandId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_DemandAssignments_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandAssignments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandTeams",
                columns: table => new
                {
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedEffortHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandTeams", x => new { x.DemandId, x.TeamId });
                    table.CheckConstraint("CK_DemandTeams_EstimatedEffortHours", "\"EstimatedEffortHours\" IS NULL OR \"EstimatedEffortHours\" > 0");
                    table.ForeignKey(
                        name: "FK_DemandTeams_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Forecasts",
                columns: table => new
                {
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPredictable = table.Column<bool>(type: "boolean", nullable: false),
                    UnpredictableReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ForecastStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDateWithoutMargin = table.Column<DateOnly>(type: "date", nullable: true),
                    ForecastEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BaselineEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forecasts", x => x.DemandId);
                    table.CheckConstraint("CK_Forecasts_EndAfterWithoutMargin", "\"ForecastEndDate\" IS NULL OR \"EndDateWithoutMargin\" IS NULL OR \"ForecastEndDate\" >= \"EndDateWithoutMargin\"");
                    table.ForeignKey(
                        name: "FK_Forecasts_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PlanningSettings",
                columns: new[] { "Id", "ExecutionOrderMode", "SafetyMarginPercent" },
                values: new object[] { 1, "FollowPriority", 20m });

            migrationBuilder.CreateIndex(
                name: "IX_DemandAssignments_PersonId",
                table: "DemandAssignments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Demands_ExecutionRank",
                table: "Demands",
                column: "ExecutionRank",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Demands_PriorityRank",
                table: "Demands",
                column: "PriorityRank",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandTeams_TeamId",
                table: "DemandTeams",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandAssignments");

            migrationBuilder.DropTable(
                name: "DemandTeams");

            migrationBuilder.DropTable(
                name: "Forecasts");

            migrationBuilder.DropTable(
                name: "PlanningSettings");

            migrationBuilder.DropTable(
                name: "Demands");
        }
    }
}
