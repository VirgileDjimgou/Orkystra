using FleetOps.Core.Modules.Operations;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class OperationsDomainTests
{
    [Fact]
    public void InspectionWithCriticalDefectBlocksDeparture()
    {
        var orgId = Guid.NewGuid();
        var inspectionId = Guid.NewGuid();
        var inspection = new PreDepartureInspection(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Critical brake issue",
            [
                new InspectionItemResult(orgId, inspectionId, 1, "brakes", "Brakes and steering", false, DefectSeverity.Critical, "Brake pedal unstable.", null),
                new InspectionItemResult(orgId, inspectionId, 2, "lights", "Lights and signals", true, DefectSeverity.None, null, null),
            ]);

        Assert.Equal(InspectionOutcome.Failed, inspection.Outcome);
        Assert.True(inspection.HasBlockingCriticalDefect);
    }

    [Fact]
    public void DeliveryProofRequiresRecipientAndSignature()
    {
        Assert.Throws<ArgumentException>(() => new DeliveryProof(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "",
            "",
            DateTimeOffset.UtcNow,
            null,
            []));
    }
}
