using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedeLocal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryVersionConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "deliveries");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "deliveries");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "deliveries",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }
    }
}
