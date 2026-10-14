using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSpare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class someImproveProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLowStockAlertEnabled",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinStockThreshold",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_MinStockThreshold_NonNegative",
                table: "Products",
                sql: "[MinStockThreshold] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_MinStockThreshold_NonNegative",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsLowStockAlertEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinStockThreshold",
                table: "Products");
        }
    }
}
