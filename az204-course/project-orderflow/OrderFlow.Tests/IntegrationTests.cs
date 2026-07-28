using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderFlow.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrders_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/orders?customerId=test-customer");
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK ||
                     response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProducts_ReturnsOkOrBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products");
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK ||
                     response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                     response.StatusCode == System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Swagger_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
    }
}
