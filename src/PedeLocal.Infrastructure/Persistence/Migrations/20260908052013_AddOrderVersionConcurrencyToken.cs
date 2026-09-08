using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedeLocal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderVersionConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "orders");
        }
    }
}
