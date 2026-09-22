using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedeLocal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Driver onboarding/documents, fixed delivery fee + radius (replacing Urban/Rural x Fixed/PerKm
    /// pricing), establishment pause, and delivery occurrences — see docs/DRIVER_ONBOARDING.md,
    /// docs/ORDER_FLOW.md and docs/DECISIONS.md.
    ///
    /// Two column groups here are RENAMED-WITH-DATA-TRANSFORM rather than a plain structural rename,
    /// because the stored values need to change meaning, not just the column name:
    ///  - drivers."ApprovalStatus": the string "PendingApproval" no longer exists as an enum member
    ///    (replaced by PendingDocuments/UnderReview) — existing rows are remapped by UPDATE.
    ///  - delivery_pricing_settings: there is no 1:1 old-to-new column, since the new DeliveryFee/
    ///    DeliveryRadiusKm are ADDED and computed from a combination of the old Urban*/MaxDistanceKm
    ///    columns (a straight column rename would silently move the wrong number into the wrong
    ///    field — e.g. price-per-km into what would become "radius"). Historical Order rows are
    ///    entirely unaffected either way: DeliveryFee/PricingRuleSnapshotJson are already snapshotted
    ///    per order at creation time.
    /// </remarks>
    public partial class AddDriverOperationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Driver onboarding: rename + remap existing status text ---
            migrationBuilder.RenameColumn(
                name: "ApprovalStatus",
                table: "drivers",
                newName: "OnboardingStatus");

            migrationBuilder.RenameColumn(
                name: "ApprovedAtUtc",
                table: "drivers",
                newName: "ReviewedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ApprovedByUserId",
                table: "drivers",
                newName: "ReviewedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "drivers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // A pre-existing "PendingApproval" driver hasn't been reviewed under the old model and,
            // under the new one, hasn't necessarily submitted CNH/vehicle documents either — the
            // safest and most conservative mapping is to require them to submit documents again
            // (PendingDocuments) rather than assume they're ready for review. Approved/Rejected are
            // unaffected (same literal value, still valid under the new enum).
            migrationBuilder.Sql(
                "UPDATE drivers SET \"OnboardingStatus\" = 'PendingDocuments' WHERE \"OnboardingStatus\" = 'PendingApproval';");

            // --- Delivery pricing: flat fee + single radius, replacing Urban/Rural x Fixed/PerKm ---
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "delivery_pricing_settings",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryRadiusKm",
                table: "delivery_pricing_settings",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 15m);

            // DeliveryFee: prefer the establishment's own configured Urban fixed fee; fall back to
            // its Urban minimum fee (already meant as a floor/base charge); fall back to a documented
            // default (R$7) only if neither was ever configured (a settings row with all zeros).
            // DeliveryRadiusKm: carries over MaxDistanceKm directly — same concept, same value,
            // simply no longer split by pricing mode.
            migrationBuilder.Sql(
                "UPDATE delivery_pricing_settings SET " +
                "\"DeliveryFee\" = CASE " +
                "WHEN \"UrbanMode\" = 'Fixed' AND \"UrbanFixedFee\" > 0 THEN \"UrbanFixedFee\" " +
                "WHEN \"UrbanMinFee\" > 0 THEN \"UrbanMinFee\" " +
                "ELSE 7.00 END, " +
                "\"DeliveryRadiusKm\" = \"MaxDistanceKm\";");

            migrationBuilder.DropColumn(name: "MaxDistanceKm", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "RuralFixedFee", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "RuralMinFee", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "RuralMode", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "RuralPricePerKm", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "UrbanFixedFee", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "UrbanMode", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "UrbanMinFee", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "UrbanPricePerKm", table: "delivery_pricing_settings");

            // --- Establishment pause (all-new, no existing data to preserve) ---
            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                table: "establishments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAtUtc",
                table: "establishments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PausedByUserId",
                table: "establishments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedUntilUtc",
                table: "establishments",
                type: "timestamp with time zone",
                nullable: true);

            // --- New tables ---
            migrationBuilder.CreateTable(
                name: "driver_licenses",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ExpiresAt = table.Column<DateOnly>(type: "date", nullable: false),
                    HasEar = table.Column<bool>(type: "boolean", nullable: true),
                    DocumentImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_licenses", x => x.DriverId);
                });

            migrationBuilder.CreateTable(
                name: "driver_vehicles",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_vehicles", x => x.DriverId);
                });

            migrationBuilder.CreateTable(
                name: "delivery_occurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_occurrences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_occurrences_DeliveryId",
                table: "delivery_occurrences",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_occurrences_DriverId",
                table: "delivery_occurrences",
                column: "DriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "delivery_occurrences");
            migrationBuilder.DropTable(name: "driver_licenses");
            migrationBuilder.DropTable(name: "driver_vehicles");

            migrationBuilder.DropColumn(name: "PauseReason", table: "establishments");
            migrationBuilder.DropColumn(name: "PausedAtUtc", table: "establishments");
            migrationBuilder.DropColumn(name: "PausedByUserId", table: "establishments");
            migrationBuilder.DropColumn(name: "PausedUntilUtc", table: "establishments");

            // Best-effort reverse mapping — not a perfect inverse (UnderReview and the original
            // Urban/Rural split have no unambiguous source to restore from), documented here rather
            // than pretending otherwise. Down is a safety net for a failed deploy, not a supported
            // round-trip of the commercial model.
            migrationBuilder.AddColumn<string>(
                name: "UrbanMode", table: "delivery_pricing_settings", type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Fixed");
            migrationBuilder.AddColumn<decimal>(
                name: "UrbanFixedFee", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "UrbanPricePerKm", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "UrbanMinFee", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>(
                name: "RuralMode", table: "delivery_pricing_settings", type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "PerKm");
            migrationBuilder.AddColumn<decimal>(
                name: "RuralFixedFee", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "RuralPricePerKm", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "RuralMinFee", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(
                name: "MaxDistanceKm", table: "delivery_pricing_settings", type: "numeric(12,2)", nullable: false, defaultValue: 15m);

            migrationBuilder.Sql(
                "UPDATE delivery_pricing_settings SET " +
                "\"UrbanFixedFee\" = \"DeliveryFee\", \"UrbanMinFee\" = \"DeliveryFee\", \"MaxDistanceKm\" = \"DeliveryRadiusKm\";");

            migrationBuilder.DropColumn(name: "DeliveryFee", table: "delivery_pricing_settings");
            migrationBuilder.DropColumn(name: "DeliveryRadiusKm", table: "delivery_pricing_settings");

            migrationBuilder.DropColumn(name: "RejectionReason", table: "drivers");

            migrationBuilder.Sql(
                "UPDATE drivers SET \"OnboardingStatus\" = 'PendingApproval' WHERE \"OnboardingStatus\" IN ('PendingDocuments', 'UnderReview');");

            migrationBuilder.RenameColumn(name: "OnboardingStatus", table: "drivers", newName: "ApprovalStatus");
            migrationBuilder.RenameColumn(name: "ReviewedAtUtc", table: "drivers", newName: "ApprovedAtUtc");
            migrationBuilder.RenameColumn(name: "ReviewedByUserId", table: "drivers", newName: "ApprovedByUserId");
        }
    }
}
