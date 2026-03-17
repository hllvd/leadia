using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class RealStateService(IRealStateRepository repository)
{
    // ── Agency CRUD ──────────────────────────────────────────────────────────

    public async Task<List<RealStateAgency>> GetAllAgenciesAsync()
    {
        return await repository.GetAllAgenciesAsync();
    }

    public async Task<RealStateAgency?> GetAgencyByIdAsync(string id)
    {
        return await repository.GetAgencyByIdAsync(id);
    }

    public async Task<RealStateAgency> CreateAgencyAsync(RealStateAgency agency)
    {
        agency.Id = Guid.NewGuid().ToString();
        await repository.AddAgencyAsync(agency);
        return agency;
    }

    public async Task<bool> UpdateAgencyAsync(RealStateAgency agency)
    {
        var existing = await repository.GetAgencyByIdAsync(agency.Id);
        if (existing == null) return false;

        existing.Name = agency.Name;
        existing.Address = agency.Address;
        existing.Description = agency.Description;

        await repository.UpdateAgencyAsync(existing);
        return true;
    }

    public async Task<bool> DeleteAgencyAsync(string id)
    {
        var existing = await repository.GetAgencyByIdAsync(id);
        if (existing == null) return false;

        await repository.DeleteAgencyAsync(id);
        return true;
    }

    // ── Broker Assignments ───────────────────────────────────────────────────

    public async Task<RealStateBroker> AssignBrokerAsync(string agencyId, string brokerId)
    {
        var assignment = new RealStateBroker
        {
            Id = Guid.NewGuid().ToString(),
            RealStateAgencyId = agencyId,
            BrokerId = brokerId
        };

        await repository.AddAssignmentAsync(assignment);
        return assignment;
    }

    public async Task<bool> RemoveAssignmentAsync(string assignmentId)
    {
        await repository.DeleteAssignmentAsync(assignmentId);
        return true;
    }

    // ── Broker Data CRUD ────────────────────────────────────────────────────

    public async Task<List<BrokerData>> GetBrokerDataAsync(string brokerId)
    {
        return await repository.GetBrokerDataAsync(brokerId);
    }

    public async Task<BrokerData> AddBrokerDataAsync(BrokerData data)
    {
        data.Id = Guid.NewGuid().ToString();
        data.CreatedAt = DateTimeOffset.UtcNow;
        data.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.AddBrokerDataAsync(data);
        return data;
    }

    public async Task<bool> UpdateBrokerDataAsync(BrokerData data)
    {
        var existing = await repository.GetBrokerDataAsync(data.BrokerId);
        var item = existing.FirstOrDefault(d => d.Id == data.Id);
        if (item == null) return false;

        item.DataName = data.DataName;
        item.DataKey = data.DataKey;
        item.DataValue = data.DataValue;
        item.IsPreferred = data.IsPreferred;
        item.Description = data.Description;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateBrokerDataAsync(item);
        return true;
    }

    public async Task<bool> DeleteBrokerDataAsync(string id)
    {
        await repository.DeleteBrokerDataAsync(id);
        return true;
    }

    public async Task<RealStateBroker?> GetAssignmentByBrokerIdAsync(string brokerId)
        => await repository.GetAssignmentsByBrokerIdAsync(brokerId);

    public async Task UpdateAssignmentAsync(RealStateBroker assignment)
        => await repository.UpdateAssignmentAsync(assignment);
}
