using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeExistingCustomerStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "State" = CASE UPPER(BTRIM("State"))
                    WHEN 'ACRE' THEN 'AC' WHEN 'ALAGOAS' THEN 'AL' WHEN 'AMAPA' THEN 'AP' WHEN 'AMAPÁ' THEN 'AP'
                    WHEN 'AMAZONAS' THEN 'AM' WHEN 'BAHIA' THEN 'BA' WHEN 'CEARA' THEN 'CE' WHEN 'CEARÁ' THEN 'CE'
                    WHEN 'DISTRITO FEDERAL' THEN 'DF' WHEN 'ESPIRITO SANTO' THEN 'ES' WHEN 'ESPÍRITO SANTO' THEN 'ES'
                    WHEN 'GOIAS' THEN 'GO' WHEN 'GOIÁS' THEN 'GO' WHEN 'MARANHAO' THEN 'MA' WHEN 'MARANHÃO' THEN 'MA'
                    WHEN 'MATO GROSSO' THEN 'MT' WHEN 'MATO GROSSO DO SUL' THEN 'MS' WHEN 'MINAS GERAIS' THEN 'MG'
                    WHEN 'PARA' THEN 'PA' WHEN 'PARÁ' THEN 'PA' WHEN 'PARAIBA' THEN 'PB' WHEN 'PARAÍBA' THEN 'PB'
                    WHEN 'PARANA' THEN 'PR' WHEN 'PARANÁ' THEN 'PR' WHEN 'PERNAMBUCO' THEN 'PE' WHEN 'PIAUI' THEN 'PI'
                    WHEN 'PIAUÍ' THEN 'PI' WHEN 'RIO DE JANEIRO' THEN 'RJ' WHEN 'RIO GRANDE DO NORTE' THEN 'RN'
                    WHEN 'RIO GRANDE DO SUL' THEN 'RS' WHEN 'RONDONIA' THEN 'RO' WHEN 'RONDÔNIA' THEN 'RO'
                    WHEN 'RORAIMA' THEN 'RR' WHEN 'SANTA CATARINA' THEN 'SC' WHEN 'SAO PAULO' THEN 'SP'
                    WHEN 'SÃO PAULO' THEN 'SP' WHEN 'SERGIPE' THEN 'SE' WHEN 'TOCANTINS' THEN 'TO'
                    ELSE UPPER(BTRIM("State")) END
                WHERE "State" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "State" = CASE UPPER(BTRIM("State"))
                    WHEN 'AC' THEN 'Acre' WHEN 'AL' THEN 'Alagoas' WHEN 'AP' THEN 'Amapá' WHEN 'AM' THEN 'Amazonas'
                    WHEN 'BA' THEN 'Bahia' WHEN 'CE' THEN 'Ceará' WHEN 'DF' THEN 'Distrito Federal'
                    WHEN 'ES' THEN 'Espírito Santo' WHEN 'GO' THEN 'Goiás' WHEN 'MA' THEN 'Maranhão'
                    WHEN 'MT' THEN 'Mato Grosso' WHEN 'MS' THEN 'Mato Grosso do Sul' WHEN 'MG' THEN 'Minas Gerais'
                    WHEN 'PA' THEN 'Pará' WHEN 'PB' THEN 'Paraíba' WHEN 'PR' THEN 'Paraná' WHEN 'PE' THEN 'Pernambuco'
                    WHEN 'PI' THEN 'Piauí' WHEN 'RJ' THEN 'Rio de Janeiro' WHEN 'RN' THEN 'Rio Grande do Norte'
                    WHEN 'RS' THEN 'Rio Grande do Sul' WHEN 'RO' THEN 'Rondônia' WHEN 'RR' THEN 'Roraima'
                    WHEN 'SC' THEN 'Santa Catarina' WHEN 'SP' THEN 'São Paulo' WHEN 'SE' THEN 'Sergipe'
                    WHEN 'TO' THEN 'Tocantins' ELSE "State" END
                WHERE "State" IS NOT NULL;
                """);
        }
    }
}
