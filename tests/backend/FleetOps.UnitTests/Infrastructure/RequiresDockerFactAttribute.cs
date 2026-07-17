using Xunit;

namespace FleetOps.UnitTests.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = $"Docker-backed Sprint 11 test skipped: {DockerAvailability.Reason}";
        }
    }
}
