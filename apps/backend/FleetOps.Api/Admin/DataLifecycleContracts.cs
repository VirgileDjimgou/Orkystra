namespace FleetOps.Api.Admin;

public sealed record DataLifecycleSummaryResponse(
    DateTimeOffset GeneratedAtUtc,
    string OrganizationName,
    string OrganizationSlug,
    int TrackingRetentionDays,
    IReadOnlyList<DataLifecycleCountResponse> Counts,
    IReadOnlyList<DataLifecycleCategoryResponse> Categories);

public sealed record DataLifecycleCountResponse(
    string Key,
    string Label,
    int Count);

public sealed record DataLifecycleCategoryResponse(
    string Key,
    string Label,
    string Description);

public sealed record PurgeLifecycleDataRequest(
    string Confirmation,
    DateTimeOffset CutoffUtc,
    string[] Categories);

public sealed record PurgeLifecycleDataResponse(
    DateTimeOffset CutoffUtc,
    int TotalDeleted,
    IReadOnlyList<PurgeLifecycleCategoryResultResponse> Results);

public sealed record PurgeLifecycleCategoryResultResponse(
    string Key,
    int DeletedCount);
