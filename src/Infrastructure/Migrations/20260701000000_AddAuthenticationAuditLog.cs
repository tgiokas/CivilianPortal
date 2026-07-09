using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CitizenPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthenticationAuditLogs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    username = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    machine_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    keycloak_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authentication_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authentication_audit_logs_created_at",
                table: "AuthenticationAuditLogs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_authentication_audit_logs_provider",
                table: "AuthenticationAuditLogs",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ix_authentication_audit_logs_success",
                table: "AuthenticationAuditLogs",
                column: "success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationAuditLogs");
        }
    }
}
