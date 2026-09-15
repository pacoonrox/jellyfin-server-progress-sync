using Jellyfin.Database.Implementations.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS1591

namespace Jellyfin.Database.Implementations.ModelConfiguration;

public class SecurityAuditRecordConfiguration : IEntityTypeConfiguration<SecurityAuditRecord>
{
    public void Configure(EntityTypeBuilder<SecurityAuditRecord> builder)
        => builder.HasIndex(x => x.TimestampUtc);
}
