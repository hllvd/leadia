using Api.Endpoints;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Integration;

public class RealStateIntegrationTests(MessagingAppTestFactory factory) : IClassFixture<MessagingAppTestFactory>
{
    private string GetAdminToken()
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        return AuthEndpoints.GenerateJwt("admin-id", "admin@test.com", "Admin", config);
    }

    private string GetUserToken()
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        return AuthEndpoints.GenerateJwt("user-id", "user@test.com", "User", config);
    }

    [Fact]
    public async Task GetAgencies_WhenNotAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetUserToken());

        // Act
        var response = await client.GetAsync("/api/realstate/agencies");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAgencies_WhenAdmin_ReturnsOk()
    {
        // Arrange
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());

        // Act
        var response = await client.GetAsync("/api/realstate/agencies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AgencyCRUD_WorkFlow()
    {
        // Arrange
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());

        // 1. Create
        var newAgency = new RealStateAgency { Name = "New Agency", Address = "Street 1", Description = "Desc" };
        var createRes = await client.PostAsJsonAsync("/api/realstate/agencies", newAgency);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<RealStateAgency>();
        Assert.NotNull(created?.Id);

        // 2. Read
        var getRes = await client.GetAsync($"/api/realstate/agencies/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<RealStateAgency>();
        Assert.Equal("New Agency", fetched?.Name);

        // 3. Update
        fetched!.Name = "Updated Name";
        var updateRes = await client.PutAsJsonAsync($"/api/realstate/agencies/{created.Id}", fetched);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);

        // 4. Delete
        var deleteRes = await client.DeleteAsync($"/api/realstate/agencies/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
    }
}
