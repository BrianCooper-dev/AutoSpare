using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSpare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class someImproveinSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItems_UnitPurchasePrice_NonNegative",
                table: "SaleItems",
                sql: "[UnitPurchasePrice] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItems_UnitPurchasePrice_NonNegative",
                table: "SaleItems");
        }
    }
}
