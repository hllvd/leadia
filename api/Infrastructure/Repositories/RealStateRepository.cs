using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RealStateRepository(AppDbContext db) : IRealStateRepository
{
    // ── Agencies ──────────────────────────────────────────────────────────

    public async Task<List<RealStateAgency>> GetAllAgenciesAsync(CancellationToken ct = default)
    {
        return await db.RealStateAgencies
            .Include(a => a.BrokerAssignments)
            .ThenInclude(ba => ba.Broker)
            .ToListAsync(ct);
    }

    public async Task<RealStateAgency?> GetAgencyByIdAsync(string id, CancellationToken ct = default)
    {
        return await db.RealStateAgencies
            .Include(a => a.BrokerAssignments)
            .ThenInclude(ba => ba.Broker)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task AddAgencyAsync(RealStateAgency agency, CancellationToken ct = default)
    {
        db.RealStateAgencies.Add(agency);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAgencyAsync(RealStateAgency agency, CancellationToken ct = default)
    {
        var existing = await db.RealStateAgencies.FindAsync(new object[] { agency.Id }, ct);
        if (existing != null)
        {
            db.Entry(existing).CurrentValues.SetValues(agency);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAgencyAsync(string id, CancellationToken ct = default)
    {
        var agency = await db.RealStateAgencies.FindAsync(new object[] { id }, ct);
        if (agency != null)
        {
            db.RealStateAgencies.Remove(agency);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Assignments ───────────────────────────────────────────────────

    public async Task AddAssignmentAsync(RealStateBroker assignment, CancellationToken ct = default)
    {
        db.RealStateBrokers.Add(assignment);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAssignmentAsync(string id, CancellationToken ct = default)
    {
        var assignment = await db.RealStateBrokers.FindAsync(new object[] { id }, ct);
        if (assignment != null)
        {
            db.RealStateBrokers.Remove(assignment);
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task<RealStateBroker?> GetAssignmentsByBrokerIdAsync(string brokerId, CancellationToken ct = default)
    {
        return await db.RealStateBrokers
            .Include(b => b.RealStateAgency)
            .FirstOrDefaultAsync(b => b.BrokerId == brokerId, ct);
    }

    public async Task UpdateAssignmentAsync(RealStateBroker assignment, CancellationToken ct = default)
    {
        var existing = await db.RealStateBrokers.FindAsync(new object[] { assignment.Id }, ct);
        if (existing != null)
        {
            db.Entry(existing).CurrentValues.SetValues(assignment);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Broker Data ────────────────────────────────────────────────────

    public async Task<List<BrokerData>> GetBrokerDataAsync(string brokerId, CancellationToken ct = default)
    {
        return await db.BrokersData
            .Where(d => d.BrokerId == brokerId)
            .OrderByDescending(d => d.IsPreferred)
            .ThenByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddBrokerDataAsync(BrokerData data, CancellationToken ct = default)
    {
        db.BrokersData.Add(data);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateBrokerDataAsync(BrokerData data, CancellationToken ct = default)
    {
        var existing = await db.BrokersData.FindAsync(new object[] { data.Id }, ct);
        if (existing != null)
        {
            db.Entry(existing).CurrentValues.SetValues(data);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteBrokerDataAsync(string id, CancellationToken ct = default)
    {
        var data = await db.BrokersData.FindAsync(new object[] { id }, ct);
        if (data != null)
        {
            db.BrokersData.Remove(data);
            await db.SaveChangesAsync(ct);
        }
    }
}
