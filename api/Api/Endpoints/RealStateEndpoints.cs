using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class RealStateEndpoints
{
    public static void MapRealStateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/realstate").RequireAuthorization("AdminOnly");

        // ── Agency Endpoints ──────────────────────────────────────────────────

        group.MapGet("/agencies", async (RealStateService service) =>
            TypedResults.Ok(await service.GetAllAgenciesAsync()));

        group.MapGet("/agencies/{id}", async (string id, RealStateService service) =>
        {
            var agency = await service.GetAgencyByIdAsync(id);
            return agency is not null ? TypedResults.Ok(agency) : Results.NotFound();
        });

        group.MapPost("/agencies", async ([FromBody] RealStateAgency agency, RealStateService service) =>
        {
            var created = await service.CreateAgencyAsync(agency);
            return TypedResults.Created($"/api/realstate/agencies/{created.Id}", created);
        });

        group.MapPut("/agencies/{id}", async (string id, [FromBody] RealStateAgency agency, RealStateService service) =>
        {
            agency.Id = id;
            var success = await service.UpdateAgencyAsync(agency);
            return success ? TypedResults.NoContent() : Results.NotFound();
        });

        group.MapDelete("/agencies/{id}", async (string id, RealStateService service) =>
        {
            var success = await service.DeleteAgencyAsync(id);
            return success ? TypedResults.NoContent() : Results.NotFound();
        });

        // ── Assignment Endpoints ──────────────────────────────────────────────

        group.MapPost("/agencies/{agencyId}/assign/{brokerId}", async (string agencyId, string brokerId, RealStateService service) =>
        {
            var assignment = await service.AssignBrokerAsync(agencyId, brokerId);
            return TypedResults.Ok(assignment);
        });

        group.MapDelete("/assignments/{id}", async (string id, RealStateService service) =>
        {
            var success = await service.RemoveAssignmentAsync(id);
            return success ? TypedResults.NoContent() : Results.NotFound();
        });

        // ── Broker Data Endpoints ─────────────────────────────────────────────

        group.MapGet("/broker-data/{brokerId}", async (string brokerId, RealStateService service) =>
            TypedResults.Ok(await service.GetBrokerDataAsync(brokerId)));

        group.MapPost("/broker-data", async ([FromBody] BrokerData data, RealStateService service) =>
        {
            var created = await service.AddBrokerDataAsync(data);
            return TypedResults.Created($"/api/realstate/broker-data/{created.Id}", created);
        });

        group.MapPut("/broker-data/{id}", async (string id, [FromBody] BrokerData data, RealStateService service) =>
        {
            data.Id = id;
            var success = await service.UpdateBrokerDataAsync(data);
            return success ? TypedResults.NoContent() : Results.NotFound();
        });

        group.MapDelete("/broker-data/{id}", async (string id, RealStateService service) =>
        {
            var success = await service.DeleteBrokerDataAsync(id);
            return success ? TypedResults.NoContent() : Results.NotFound();
        });
        group.MapPut("/brokers/{brokerId}/mode", async (string brokerId, [FromBody] ConversationMode mode, RealStateService service) =>
        {
            var assignment = await service.GetAssignmentByBrokerIdAsync(brokerId);
            if (assignment is null) return Results.NotFound();
            assignment.Mode = mode;
            await service.UpdateAssignmentAsync(assignment);
            return TypedResults.NoContent();
        });
    }
}
