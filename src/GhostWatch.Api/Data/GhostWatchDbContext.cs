using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Eve;
using GhostWatch.Api.Eve.Inventory;
using GhostWatch.Api.Eve.Esi;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Data;

public sealed class GhostWatchDbContext(DbContextOptions<GhostWatchDbContext> options) : DbContext(options)
{
    public DbSet<EveLocationName> EveLocationNames => Set<EveLocationName>();
    public DbSet<PublicEveLookup> PublicEveLookups => Set<PublicEveLookup>();
    public DbSet<EveSection> EveSections => Set<EveSection>();
    public DbSet<EveIndustryJob> EveIndustryJobs => Set<EveIndustryJob>();
    public DbSet<EveCharacter> EveCharacters => Set<EveCharacter>();
    public DbSet<EconomyTrack> EconomyTracks => Set<EconomyTrack>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var location = model.Entity<EveLocationName>();
        location.HasKey(x => new { x.CharacterId, x.LocationId });
        location.HasOne<EveCharacter>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        location.Property(x => x.ExpiresAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        model.Entity<PublicEveLookup>().HasKey(x => x.Key);
        model.Entity<PublicEveLookup>().Property(x => x.ExpiresAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var section = model.Entity<EveSection>();
        section.HasKey(x => new { x.CharacterId, x.Name });
        section.HasOne<EveCharacter>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        var job = model.Entity<EveIndustryJob>();
        job.HasKey(x => new { x.CharacterId, x.JobId });
        job.HasOne<EveCharacter>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        section.Property(x => x.AttemptedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        section.Property(x => x.UpdatedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        job.Property(x => x.StartDate).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        job.Property(x => x.EndDate).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        job.Property(x => x.LastSeenAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var character = model.Entity<EveCharacter>();
        character.ToTable("EveCharacters");
        character.HasKey(x => x.CharacterId);
        character.Property(x => x.CharacterId).ValueGeneratedNever();
        character.Property(x => x.ConnectedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        character.Property(x => x.LastAuthenticatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
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
