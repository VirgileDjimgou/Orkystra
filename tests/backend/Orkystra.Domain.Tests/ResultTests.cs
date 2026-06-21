using Orkystra.Domain.Common;

namespace Orkystra.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void FailureResult_DoesNotExposeValue()
    {
        var result = Result.Failure<int>(DomainErrors.InvalidValue("quantity", "must be positive"));

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void SuccessResult_ExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }
}
