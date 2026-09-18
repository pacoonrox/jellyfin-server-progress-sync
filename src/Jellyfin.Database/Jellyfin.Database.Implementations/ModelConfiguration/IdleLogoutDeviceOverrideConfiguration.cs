using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS1591

namespace Jellyfin.Database.Implementations.ModelConfiguration;

public class IdleLogoutDeviceOverrideConfiguration : IEntityTypeConfiguration<IdleLogoutDeviceOverride>
{
    public void Configure(EntityTypeBuilder<IdleLogoutDeviceOverride> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();
    }
}
