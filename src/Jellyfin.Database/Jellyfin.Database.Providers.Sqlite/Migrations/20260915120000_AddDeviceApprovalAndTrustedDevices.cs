using System;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations;

/// <inheritdoc />
[DbContext(typeof(JellyfinDbContext))]
[Migration("20260915120000_AddDeviceApprovalAndTrustedDevices")]
public partial class AddDeviceApprovalAndTrustedDevices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "AuthenticationProvenance", table: "Devices", type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Legacy");
        migrationBuilder.AddColumn<DateTime>(name: "DirectTwoFactorVerifiedUtc", table: "Devices", type: "TEXT", nullable: true);

        migrationBuilder.CreateTable(
            name: "SecurityAuditRecords",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ActingUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                TargetUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                TrustedDeviceId = table.Column<long>(type: "INTEGER", nullable: true),
                Event = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Result = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                AdministratorInvolved = table.Column<bool>(type: "INTEGER", nullable: false),
                Detail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SecurityAuditRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TrustedDevices",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CredentialHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                InstallationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FriendlyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                AppName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                AppVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Platform = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                OsVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                LastIpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                IssuedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                RequiresFreshTwoFactor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_TrustedDevices", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_SecurityAuditRecords_TimestampUtc", table: "SecurityAuditRecords", column: "TimestampUtc");
        migrationBuilder.CreateIndex(name: "IX_TrustedDevices_ExpiresUtc", table: "TrustedDevices", column: "ExpiresUtc");
        migrationBuilder.CreateIndex(name: "IX_TrustedDevices_UserId_InstallationId", table: "TrustedDevices", columns: new[] { "UserId", "InstallationId" });
        migrationBuilder.CreateIndex(name: "IX_TrustedDevices_UserId_CredentialHash", table: "TrustedDevices", columns: new[] { "UserId", "CredentialHash" }, unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SecurityAuditRecords");
        migrationBuilder.DropTable(name: "TrustedDevices");
        migrationBuilder.DropColumn(name: "AuthenticationProvenance", table: "Devices");
        migrationBuilder.DropColumn(name: "DirectTwoFactorVerifiedUtc", table: "Devices");
    }
}
