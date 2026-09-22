namespace GhostWatch.Api.Knowledge;
public sealed class Playbook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MarkdownBody { get; set; } = "";
    public string TagsJson { get; set; } = "[]";
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}
public sealed class PlaybookRevision
{
    public Guid PlaybookId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = "";
    public string MarkdownBody { get; set; } = "";
    public DateTime SavedAt { get; set; }
}
public sealed class EconomicRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string RecordType { get; set; } = "Note";
    public string MarkdownBody { get; set; } = "";
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}
// Exactly one knowledge owner and one related object; all references have restrictive FKs.
public sealed class KnowledgeLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PlaybookId { get; set; }
    public Guid? RecordId { get; set; }
    public Guid? TrackId { get; set; }
    public Guid? RunId { get; set; }
    public long? CharacterId { get; set; }
    public Guid? RelatedPlaybookId { get; set; }
    public Guid? ObjectiveId { get; set; }
    public Guid? CapitalPoolId { get; set; }
}
