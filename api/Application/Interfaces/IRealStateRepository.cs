using Domain.Entities;

namespace Application.Interfaces;

public interface IRealStateRepository
{
    // Agencies
    Task<List<RealStateAgency>> GetAllAgenciesAsync(CancellationToken ct = default);
    Task<RealStateAgency?> GetAgencyByIdAsync(string id, CancellationToken ct = default);
    Task AddAgencyAsync(RealStateAgency agency, CancellationToken ct = default);
    Task UpdateAgencyAsync(RealStateAgency agency, CancellationToken ct = default);
    Task DeleteAgencyAsync(string id, CancellationToken ct = default);

    // Assignments
    Task AddAssignmentAsync(RealStateBroker assignment, CancellationToken ct = default);
    Task DeleteAssignmentAsync(string id, CancellationToken ct = default);
    Task<RealStateBroker?> GetAssignmentsByBrokerIdAsync(string brokerId, CancellationToken ct = default);
    Task UpdateAssignmentAsync(RealStateBroker assignment, CancellationToken ct = default);

    // Broker Data
    Task<List<BrokerData>> GetBrokerDataAsync(string brokerId, CancellationToken ct = default);
    Task AddBrokerDataAsync(BrokerData data, CancellationToken ct = default);
    Task UpdateBrokerDataAsync(BrokerData data, CancellationToken ct = default);
    Task DeleteBrokerDataAsync(string id, CancellationToken ct = default);
}
