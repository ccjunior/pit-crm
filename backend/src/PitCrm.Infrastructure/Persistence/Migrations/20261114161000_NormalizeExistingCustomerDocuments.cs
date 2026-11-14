using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PitCrm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PitCrmDbContext))]
[Migration("20261114161000_NormalizeExistingCustomerDocuments")]
public sealed class NormalizeExistingCustomerDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE "Customers"
        SET "Document" = regexp_replace("Document", '[^0-9]', '', 'g')
        WHERE "Document" IS NOT NULL
          AND "Document" ~ '^[-0-9./[:space:]]+$'
          AND "Document" ~ '[0-9]';
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A canonicalização de pontuação não pode ser revertida sem armazenar o formato anterior.
    }
}
