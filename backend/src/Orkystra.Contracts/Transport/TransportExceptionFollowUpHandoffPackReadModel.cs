namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpHandoffPackReadModel(
    int ActiveItemCount,
    int ImmediateCount,
    int ThisShiftCount,
    int NextShiftCount,
    int MissingOwnerCount,
    int MissingNoteCount,
    int MissingRouteContextCount,
    string Summary,
    string ShiftSummary,
    string OwnerHeadline,
    string AcknowledgementHeadline,
    IReadOnlyCollection<string> BriefingLines,
    IReadOnlyCollection<TransportExceptionFollowUpHandoffItemReadModel> Items);
