using FleetOps.Core.Modules.Fleet;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DriverTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void ConstructorNormalizesWhitespace()
    {
        var driver = new Driver(OrgId, "  Jane Doe  ", "  DL-100  ", "  +1 555  ");

        Assert.Equal("Jane Doe", driver.FullName);
        Assert.Equal("DL-100", driver.LicenseNumber);
        Assert.Equal("+1 555", driver.PhoneNumber);
        Assert.True(driver.IsActive);
        Assert.Equal(OrgId, driver.OrganizationId);
    }

    [Fact]
    public void ConstructorRejectsEmptyOrganization()
    {
        Assert.Throws<ArgumentException>(() => new Driver(Guid.Empty, "Jane", "DL-100"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankFullName(string fullName)
    {
        Assert.Throws<ArgumentException>(() => new Driver(OrgId, fullName, "DL-100"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankLicenseNumber(string licenseNumber)
    {
        Assert.Throws<ArgumentException>(() => new Driver(OrgId, "Jane", licenseNumber));
    }

    [Fact]
    public void PhoneNumberNullOrWhitespaceBecomesNull()
    {
        var driver = new Driver(OrgId, "Jane", "DL-100", "   ");

        Assert.Null(driver.PhoneNumber);
    }

    [Fact]
    public void ActivateTwiceThrows()
    {
        var driver = new Driver(OrgId, "Jane", "DL-100");

        driver.Deactivate();
        driver.Activate();

        Assert.Throws<InvalidOperationException>(driver.Activate);
    }

    [Fact]
    public void DeactivateTwiceThrows()
    {
        var driver = new Driver(OrgId, "Jane", "DL-100");

        driver.Deactivate();

        Assert.Throws<InvalidOperationException>(driver.Deactivate);
    }

    [Fact]
    public void UpdateOverwritesNameAndPhone()
    {
        var driver = new Driver(OrgId, "Jane", "DL-100", "+1");

        driver.Update("  Jane Roe  ", null);

        Assert.Equal("Jane Roe", driver.FullName);
        Assert.Null(driver.PhoneNumber);
    }
}

public sealed class GpsDeviceTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void ConstructorNormalizesValues()
    {
        var device = new GpsDevice(OrgId, "  SNO-001  ", "  Trailer tracker  ");

        Assert.Equal("SNO-001", device.SerialNumber);
        Assert.Equal("Trailer tracker", device.DisplayName);
        Assert.True(device.IsActive);
    }

    [Fact]
    public void ConstructorRejectsEmptyOrganization()
    {
        Assert.Throws<ArgumentException>(() => new GpsDevice(Guid.Empty, "SNO-001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankSerial(string serial)
    {
        Assert.Throws<ArgumentException>(() => new GpsDevice(OrgId, serial));
    }

    [Fact]
    public void RenameAcceptsNull()
    {
        var device = new GpsDevice(OrgId, "SNO-001", "Initial");

        device.Rename(null);

        Assert.Null(device.DisplayName);
    }

    [Fact]
    public void ActivateTwiceThrows()
    {
        var device = new GpsDevice(OrgId, "SNO-001");

        Assert.Throws<InvalidOperationException>(device.Activate);
    }

    [Fact]
    public void DeactivateTwiceThrows()
    {
        var device = new GpsDevice(OrgId, "SNO-001");

        device.Deactivate();

        Assert.Throws<InvalidOperationException>(device.Deactivate);
    }
}

public sealed class VehicleTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void ConstructorNormalizesValues()
    {
        var vehicle = new Vehicle(OrgId, "  NW-100  ", "  Dispatch van  ");

        Assert.Equal("NW-100", vehicle.RegistrationNumber);
        Assert.Equal("Dispatch van", vehicle.DisplayName);
        Assert.True(vehicle.IsActive);
    }

    [Fact]
    public void ConstructorRejectsEmptyOrganization()
    {
        Assert.Throws<ArgumentException>(() => new Vehicle(Guid.Empty, "NW-100", "Van"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankRegistrationNumber(string registration)
    {
        Assert.Throws<ArgumentException>(() => new Vehicle(OrgId, registration, "Van"));
    }

    [Fact]
    public void RenameRejectsBlank()
    {
        var vehicle = new Vehicle(OrgId, "NW-100", "Van");

        Assert.Throws<ArgumentException>(() => vehicle.Rename("   "));
    }

    [Fact]
    public void StateTransitionsAreIdempotent()
    {
        var vehicle = new Vehicle(OrgId, "NW-100", "Van");

        vehicle.Deactivate();

        Assert.False(vehicle.IsActive);

        vehicle.Activate();

        Assert.True(vehicle.IsActive);

        Assert.Throws<InvalidOperationException>(vehicle.Activate);
        Assert.Throws<InvalidOperationException>(() =>
        {
            vehicle.Deactivate();
            vehicle.Deactivate();
        });
    }
}

public sealed class DeviceAssignmentTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly DateTimeOffset AssignedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorStoresUniversalTimeAndFlagsActive()
    {
        var assignment = new DeviceAssignment(OrgId, Guid.NewGuid(), Guid.NewGuid(), AssignedAt);

        Assert.Equal(AssignedAt, assignment.AssignedAtUtc);
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.UnassignedAtUtc);
    }

    [Fact]
    public void ConstructorRejectsEmptyIdentifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            new DeviceAssignment(OrgId, Guid.Empty, Guid.NewGuid(), AssignedAt));
        Assert.Throws<ArgumentException>(() =>
            new DeviceAssignment(OrgId, Guid.NewGuid(), Guid.Empty, AssignedAt));
        Assert.Throws<ArgumentException>(() =>
            new DeviceAssignment(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), AssignedAt));
    }

    [Fact]
    public void CloseSetsUnassignedAt()
    {
        var assignment = new DeviceAssignment(OrgId, Guid.NewGuid(), Guid.NewGuid(), AssignedAt);

        assignment.Close(AssignedAt.AddHours(1));

        Assert.False(assignment.IsActive);
        Assert.Equal(AssignedAt.AddHours(1), assignment.UnassignedAtUtc);
    }

    [Fact]
    public void CloseTwiceThrows()
    {
        var assignment = new DeviceAssignment(OrgId, Guid.NewGuid(), Guid.NewGuid(), AssignedAt);

        assignment.Close(AssignedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => assignment.Close(AssignedAt.AddHours(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CloseRejectsTimeBeforeAssignment(int minuteOffset)
    {
        var assignment = new DeviceAssignment(OrgId, Guid.NewGuid(), Guid.NewGuid(), AssignedAt);

        Assert.Throws<ArgumentException>(() => assignment.Close(AssignedAt.AddMinutes(minuteOffset - 1)));
    }
}
