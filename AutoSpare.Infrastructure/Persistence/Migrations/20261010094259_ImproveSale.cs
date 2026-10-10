using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSpare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImproveSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPurchasePrice",
                table: "SaleItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitPurchasePrice",
                table: "SaleItems");
        }
    }
}
