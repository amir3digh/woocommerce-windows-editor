namespace WooCommerceProductManager.Services;

public sealed class SyncProgress
{
    public int Downloaded { get; init; }

    public int Updated { get; init; }

    public int Added { get; init; }

    public int Conflicts { get; init; }

    public int Errors { get; init; }

    public int CurrentPage { get; init; }

    public int? TotalPages { get; init; }

    public bool IsComplete { get; init; }

    public string? StatusMessage { get; init; }
}

public sealed class SyncFromWebsiteResult
{
    public int Downloaded { get; init; }

    public int Updated { get; init; }

    public int Added { get; init; }

    public int Conflicts { get; init; }

    public int Errors { get; init; }

    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> ConflictSummaries { get; init; } = [];
}
