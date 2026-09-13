using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WooCommerceProductManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    LocalId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WooCommerceId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RegularPrice = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SalePrice = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Price = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ManageStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    StockStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    DateModified = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDirty = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.LocalId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_WooCommerceId",
                table: "Products",
                column: "WooCommerceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
