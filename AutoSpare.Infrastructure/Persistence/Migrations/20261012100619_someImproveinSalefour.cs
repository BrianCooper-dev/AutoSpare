using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSpare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class someImproveinSalefour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItems_UnitPurchasePrice_NonNegative",
                table: "SaleItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItems_UnitPurchasePrice_NonNegative",
                table: "SaleItems",
                sql: "[UnitPurchasePrice] >= 0");
        }
    }
}
