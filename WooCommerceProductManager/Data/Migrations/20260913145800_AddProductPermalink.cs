using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WooCommerceProductManager.Data.Migrations;

/// <inheritdoc />
public partial class AddProductPermalink : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Permalink",
            table: "Products",
            type: "TEXT",
            maxLength: 2048,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Permalink",
            table: "Products");
    }
}
