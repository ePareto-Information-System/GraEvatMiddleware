using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraEvatMiddleware.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationExchangeLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_exchange_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DestinationSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RequestHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestBody = table.Column<string>(type: "text", nullable: true),
                    RequestBodyTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    ResponseBodyTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_exchange_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_exchange_logs_CorrelationId",
                table: "integration_exchange_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_exchange_logs_Operation_CreatedAt",
                table: "integration_exchange_logs",
                columns: new[] { "Operation", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_exchange_logs_Outcome_CreatedAt",
                table: "integration_exchange_logs",
                columns: new[] { "Outcome", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_exchange_logs_TenantId_CreatedAt",
                table: "integration_exchange_logs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_exchange_logs_TransactionId",
                table: "integration_exchange_logs",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_exchange_logs");
        }
    }
}
