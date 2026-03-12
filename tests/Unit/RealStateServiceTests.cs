using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Moq;
using Xunit;

namespace Unit;

public class RealStateServiceTests
{
    private readonly Mock<IRealStateRepository> _repoMock;
    private readonly RealStateService _service;

    public RealStateServiceTests()
    {
        _repoMock = new Mock<IRealStateRepository>();
        _service = new RealStateService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateAgencyAsync_SavesToDatabase()
    {
        // Arrange
        var agency = new RealStateAgency { Name = "Test Agency", Address = "123 Street" };

        // Act
        var created = await _service.CreateAgencyAsync(agency);

        // Assert
        Assert.NotNull(created.Id);
        _repoMock.Verify(r => r.AddAgencyAsync(It.IsAny<RealStateAgency>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAgencyAsync_ModifiesExistingRecord()
    {
        // Arrange
        var agency = new RealStateAgency { Id = "1", Name = "New Name" };
        _repoMock.Setup(r => r.GetAgencyByIdAsync("1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealStateAgency { Id = "1", Name = "Old Name" });

        // Act
        var success = await _service.UpdateAgencyAsync(agency);

        // Assert
        Assert.True(success);
        _repoMock.Verify(r => r.UpdateAgencyAsync(It.Is<RealStateAgency>(a => a.Name == "New Name"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAgencyAsync_RemovesFromDatabase()
    {
        // Arrange
        var agencyId = "1";
        _repoMock.Setup(r => r.GetAgencyByIdAsync(agencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealStateAgency { Id = agencyId });

        // Act
        var success = await _service.DeleteAgencyAsync(agencyId);

        // Assert
        Assert.True(success);
        _repoMock.Verify(r => r.DeleteAgencyAsync(agencyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignBrokerAsync_CreatesLink()
    {
        // Arrange
        var agencyId = "a1";
        var brokerId = "b1";

        // Act
        var assignment = await _service.AssignBrokerAsync(agencyId, brokerId);

        // Assert
        Assert.NotNull(assignment.Id);
        Assert.Equal(agencyId, assignment.RealStateAgencyId);
        Assert.Equal(brokerId, assignment.BrokerId);
        _repoMock.Verify(r => r.AddAssignmentAsync(It.IsAny<RealStateBroker>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddBrokerDataAsync_SavesWithRequiredFields()
    {
        // Arrange
        var data = new BrokerData 
        { 
            BrokerId = "b1",
            DataName = "Work Phone",
            DataKey = "phone",
            DataValue = "+5511"
        };

        // Act
        var created = await _service.AddBrokerDataAsync(data);

        // Assert
        Assert.NotNull(created.Id);
        _repoMock.Verify(r => r.AddBrokerDataAsync(It.IsAny<BrokerData>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
