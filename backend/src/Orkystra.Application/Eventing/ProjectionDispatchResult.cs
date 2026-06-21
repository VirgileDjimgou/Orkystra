namespace Orkystra.Application.Eventing;

public sealed record ProjectionDispatchResult(
    IReadOnlyCollection<string> AppliedProjections,
    IReadOnlyCollection<string> SkippedProjections)
{
    public int AppliedCount => AppliedProjections.Count;

    public int SkippedCount => SkippedProjections.Count;
}
