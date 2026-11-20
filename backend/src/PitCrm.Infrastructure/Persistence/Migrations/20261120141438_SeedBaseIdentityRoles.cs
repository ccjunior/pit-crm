using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedBaseIdentityRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES
                    ('10000000-0000-0000-0000-000000000001', 'Administrador', 'ADMINISTRADOR', '10000000-0000-0000-0000-000000000101'),
                    ('10000000-0000-0000-0000-000000000002', 'Gestor', 'GESTOR', '10000000-0000-0000-0000-000000000102'),
                    ('10000000-0000-0000-0000-000000000003', 'Operador', 'OPERADOR', '10000000-0000-0000-0000-000000000103')
                ON CONFLICT ("NormalizedName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AspNetRoles" AS seeded_role
                WHERE seeded_role."Id" IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AspNetUserRoles" AS user_role
                      WHERE user_role."RoleId" = seeded_role."Id");
                """);
        }
    }
}
