using GhostWatch.Api.Economics.Tracks;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Data;

public sealed class GhostWatchDbContext(DbContextOptions<GhostWatchDbContext> options) : DbContext(options)
{
    public DbSet<EconomyTrack> EconomyTracks => Set<EconomyTrack>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var track = model.Entity<EconomyTrack>();
        track.ToTable("EconomyTracks");
        track.HasKey(x => x.Id);
        track.Property(x => x.Name).HasMaxLength(120);
        track.Property(x => x.Description).HasMaxLength(2000);
        track.Property(x => x.Notes).HasMaxLength(20000);
        track.Property(x => x.Status).HasMaxLength(20);
        track.Property(x => x.Purpose).HasMaxLength(30);
        track.Property(x => x.Revision).IsConcurrencyToken();
        track.HasIndex(x => x.Status);
        track.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        track.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        track.Property(x => x.ArchivedAt).HasConversion(v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
    }
}
