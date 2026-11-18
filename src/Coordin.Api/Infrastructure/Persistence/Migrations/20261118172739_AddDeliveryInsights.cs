using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordin.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualEffortHours",
                table: "Demands",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedEffortHoursAtCompletion",
                table: "Demands",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ForecastEndDateAtCompletion",
                table: "Demands",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EstimateHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    NewHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimateHistory", x => x.Id);
                    table.CheckConstraint("CK_EstimateHistory_Changed", "\"PreviousHours\" IS DISTINCT FROM \"NewHours\"");
                    table.ForeignKey(
                        name: "FK_EstimateHistory_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForecastHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Cause = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastHistory", x => x.Id);
                    table.CheckConstraint("CK_ForecastHistory_Changed", "\"PreviousEndDate\" IS DISTINCT FROM \"NewEndDate\"");
                    table.ForeignKey(
                        name: "FK_ForecastHistory_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Demands_ActualEffortHours",
                table: "Demands",
                sql: "\"ActualEffortHours\" IS NULL OR \"ActualEffortHours\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_EstimateHistory_DemandId_ChangedAt",
                table: "EstimateHistory",
                columns: new[] { "DemandId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ForecastHistory_DemandId_ChangedAt",
                table: "ForecastHistory",
                columns: new[] { "DemandId", "ChangedAt" });
            // Somente inclusão (DT-14): atualização sempre recusada; exclusão só pela cascata da demanda, que roda
            // dentro de um gatilho (pg_trigger_depth() > 0). Apagar um registro direto no banco é recusado.
            migrationBuilder.Sql("""
                CREATE FUNCTION coordin_refuse_history_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'histórico é somente de inclusão e não pode ser alterado'
                            USING ERRCODE = 'check_violation';
                    END IF;

                    -- Dentro deste gatilho a profundidade já é 1; a cascata da demanda chega com 2 ou mais.
                    IF pg_trigger_depth() <= 1 THEN
                        RAISE EXCEPTION 'histórico é somente de inclusão e não pode ser apagado com a demanda existente'
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN OLD;
                END;
                $$;

                CREATE TRIGGER "TR_ForecastHistory_Immutable"
                    BEFORE UPDATE OR DELETE ON "ForecastHistory"
                    FOR EACH ROW EXECUTE FUNCTION coordin_refuse_history_change();

                CREATE TRIGGER "TR_EstimateHistory_Immutable"
                    BEFORE UPDATE OR DELETE ON "EstimateHistory"
                    FOR EACH ROW EXECUTE FUNCTION coordin_refuse_history_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_ForecastHistory_Immutable" ON "ForecastHistory";
                DROP TRIGGER IF EXISTS "TR_EstimateHistory_Immutable" ON "EstimateHistory";
                DROP FUNCTION IF EXISTS coordin_refuse_history_change();
                """);

            migrationBuilder.DropTable(
                name: "EstimateHistory");

            migrationBuilder.DropTable(
                name: "ForecastHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Demands_ActualEffortHours",
                table: "Demands");

            migrationBuilder.DropColumn(
                name: "ActualEffortHours",
                table: "Demands");

            migrationBuilder.DropColumn(
                name: "EstimatedEffortHoursAtCompletion",
                table: "Demands");

            migrationBuilder.DropColumn(
                name: "ForecastEndDateAtCompletion",
                table: "Demands");
        }
    }
}
