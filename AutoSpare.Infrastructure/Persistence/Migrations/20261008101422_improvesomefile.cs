using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSpare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class improvesomefile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "SaleItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId_ProductId_WarehouseId",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_WarehouseId",
                table: "SaleItems",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Warehouses_WarehouseId",
                table: "SaleItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Warehouses_WarehouseId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_SaleId_ProductId_WarehouseId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_WarehouseId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "SaleItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
