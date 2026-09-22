using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Management;
using GhostWatch.Api.Eve;
using GhostWatch.Api.Eve.Inventory;
using GhostWatch.Api.Eve.Esi;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Data;

public sealed class GhostWatchDbContext(DbContextOptions<GhostWatchDbContext> options) : DbContext(options)
{
    public DbSet<EconomicRun> EconomicRuns => Set<EconomicRun>();
    public DbSet<RunJob> RunJobs => Set<RunJob>();
    public DbSet<CapitalPool> CapitalPools => Set<CapitalPool>();
    public DbSet<CapitalAdjustment> CapitalAdjustments => Set<CapitalAdjustment>();
    public DbSet<ManagedAccount> ManagedAccounts => Set<ManagedAccount>();
    public DbSet<CharacterPlan> CharacterPlans => Set<CharacterPlan>();
    public DbSet<CharacterTrack> CharacterTracks => Set<CharacterTrack>();
    public DbSet<EveLocationName> EveLocationNames => Set<EveLocationName>();
    public DbSet<PublicEveLookup> PublicEveLookups => Set<PublicEveLookup>();
    public DbSet<EveSection> EveSections => Set<EveSection>();
    public DbSet<EveIndustryJob> EveIndustryJobs => Set<EveIndustryJob>();
    public DbSet<EveCharacter> EveCharacters => Set<EveCharacter>();
    public DbSet<EconomyTrack> EconomyTracks => Set<EconomyTrack>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var run = model.Entity<EconomicRun>(); run.HasKey(x => x.Id); run.Property(x => x.Revision).IsConcurrencyToken();
        run.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne(x => x.CapitalPool).WithMany().HasForeignKey(x => x.CapitalPoolId).OnDelete(DeleteBehavior.Restrict);
        run.Property(x => x.StartedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        run.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        run.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        run.Property(x => x.CompletedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        var runJob = model.Entity<RunJob>(); runJob.HasKey(x => new { x.CharacterId, x.JobId });
        runJob.HasOne(x => x.Run).WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        runJob.HasOne(x => x.Job).WithMany().HasForeignKey(x => new { x.CharacterId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
        var pool = model.Entity<CapitalPool>(); pool.HasKey(x => x.Id); pool.Property(x => x.Revision).IsConcurrencyToken();
        pool.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        pool.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        pool.Property(x => x.ArchivedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        pool.HasIndex(x => x.Role).IsUnique().HasFilter("\"Role\" <> 'Other' AND \"ArchivedAt\" IS NULL");
        var adjustment = model.Entity<CapitalAdjustment>(); adjustment.HasKey(x => x.Id);
        adjustment.HasOne<CapitalPool>().WithMany().HasForeignKey(x => x.FromPoolId).OnDelete(DeleteBehavior.Restrict);
        adjustment.HasOne<CapitalPool>().WithMany().HasForeignKey(x => x.ToPoolId).OnDelete(DeleteBehavior.Restrict);
        adjustment.Property(x => x.Date).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        adjustment.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var account = model.Entity<ManagedAccount>();
        account.HasKey(x => x.Id); account.Property(x => x.Revision).IsConcurrencyToken();
        var plan = model.Entity<CharacterPlan>();
        plan.HasKey(x => x.CharacterId); plan.Property(x => x.CharacterId).ValueGeneratedNever(); plan.Property(x => x.Revision).IsConcurrencyToken();
        plan.HasOne<EveCharacter>().WithOne().HasForeignKey<CharacterPlan>(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        plan.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        var link = model.Entity<CharacterTrack>(); link.HasKey(x => new { x.CharacterId, x.TrackId });
        link.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);
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
        track.HasOne<CapitalPool>().WithMany().HasForeignKey(x => x.DefaultCapitalPoolId).OnDelete(DeleteBehavior.Restrict);
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
