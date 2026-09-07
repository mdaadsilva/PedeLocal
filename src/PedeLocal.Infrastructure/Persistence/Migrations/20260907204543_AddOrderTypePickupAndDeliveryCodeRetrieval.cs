using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedeLocal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTypePickupAndDeliveryCodeRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AddressId",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Delivery");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedCode",
                table: "delivery_confirmations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "EncryptedCode",
                table: "delivery_confirmations");

            migrationBuilder.AlterColumn<Guid>(
                name: "AddressId",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
