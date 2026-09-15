using Jellyfin.Database.Implementations.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS1591

namespace Jellyfin.Database.Implementations.ModelConfiguration;

public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.CredentialHash }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.InstallationId });
        builder.HasIndex(x => x.ExpiresUtc);
    }
}
