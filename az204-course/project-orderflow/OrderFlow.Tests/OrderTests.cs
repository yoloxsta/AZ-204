using FluentAssertions;
using Moq;
using Xunit;

namespace OrderFlow.Tests;

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string Status { get; set; } = OrderStatus.Pending.ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);

    public void SetPartitionKey(string customerId)
    {
        CustomerId = customerId;
    }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public interface IOrderProcessingService
{
    Task<Order> CreateOrderAsync(string customerId, List<OrderItem> items);
}

public class OrderProcessingService : IOrderProcessingService
{
    public Task<Order> CreateOrderAsync(string customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            CustomerId = customerId,
            Items = items,
            Status = OrderStatus.Pending.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Task.FromResult(order);
    }
}

public class OrderTests
{
    [Fact]
    public void Order_Creation_ShouldHaveDefaultValues()
    {
        var order = new Order();

        order.Id.Should().NotBeNullOrEmpty();
        order.Status.Should().Be(OrderStatus.Pending.ToString());
        order.Items.Should().BeEmpty();
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Order_Creation_ShouldGenerateUniqueIds()
    {
        var order1 = new Order();
        var order2 = new Order();

        order1.Id.Should().NotBe(order2.Id);
    }

    [Fact]
    public void SetPartitionKey_ShouldSetCustomerId()
    {
        var order = new Order();
        var customerId = "customer-123";

        order.SetPartitionKey(customerId);

        order.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public void TotalAmount_WithNoItems_ShouldReturnZero()
    {
        var order = new Order();

        order.TotalAmount.Should().Be(0);
    }

    [Fact]
    public void TotalAmount_WithSingleItem_ShouldCalculateCorrectly()
    {
        var order = new Order
        {
            Items = new List<OrderItem>
            {
                new() { ProductId = "p1", ProductName = "Widget", Price = 29.99m, Quantity = 2 }
            }
        };

        order.TotalAmount.Should().Be(59.98m);
    }

    [Fact]
    public void TotalAmount_WithMultipleItems_ShouldSumAll()
    {
        var order = new Order
        {
            Items = new List<OrderItem>
            {
                new() { ProductId = "p1", ProductName = "Widget", Price = 10.00m, Quantity = 2 },
                new() { ProductId = "p2", ProductName = "Gadget", Price = 25.50m, Quantity = 1 },
                new() { ProductId = "p3", ProductName = "Doohickey", Price = 5.00m, Quantity = 3 }
            }
        };

        order.TotalAmount.Should().Be(60.50m);
    }

    [Fact]
    public void TotalAmount_WithZeroQuantity_ShouldReturnZero()
    {
        var order = new Order
        {
            Items = new List<OrderItem>
            {
                new() { ProductId = "p1", ProductName = "Widget", Price = 29.99m, Quantity = 0 }
            }
        };

        order.TotalAmount.Should().Be(0);
    }

    [Fact]
    public void OrderStatus_ShouldHaveCorrectValues()
    {
        var pending = OrderStatus.Pending;
        var processing = OrderStatus.Processing;
        var completed = OrderStatus.Completed;
        var cancelled = OrderStatus.Cancelled;

        pending.ToString().Should().Be("Pending");
        processing.ToString().Should().Be("Processing");
        completed.ToString().Should().Be("Completed");
        cancelled.ToString().Should().Be("Cancelled");
    }

    [Fact]
    public void OrderStatus_ShouldHaveFourValues()
    {
        var values = Enum.GetValues<OrderStatus>();

        values.Should().HaveCount(4);
    }

    [Fact]
    public async Task OrderProcessingService_CreateOrderAsync_ShouldCreateOrder()
    {
        var service = new OrderProcessingService();
        var customerId = "cust-001";
        var items = new List<OrderItem>
        {
            new() { ProductId = "p1", ProductName = "Widget", Price = 15.00m, Quantity = 4 }
        };

        var order = await service.CreateOrderAsync(customerId, items);

        order.Should().NotBeNull();
        order.CustomerId.Should().Be(customerId);
        order.Items.Should().HaveCount(1);
        order.Status.Should().Be(OrderStatus.Pending.ToString());
        order.TotalAmount.Should().Be(60.00m);
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OrderProcessingService_CreateOrderAsync_ShouldSetPendingStatus()
    {
        var service = new OrderProcessingService();

        var order = await service.CreateOrderAsync("cust-002", new List<OrderItem>());

        order.Status.Should().Be(OrderStatus.Pending.ToString());
    }

    [Fact]
    public async Task OrderProcessingService_MockShouldWork()
    {
        var mockService = new Mock<IOrderProcessingService>();
        var expectedOrder = new Order
        {
            CustomerId = "mock-customer",
            Status = OrderStatus.Pending.ToString()
        };

        mockService
            .Setup(s => s.CreateOrderAsync(It.IsAny<string>(), It.IsAny<List<OrderItem>>()))
            .ReturnsAsync(expectedOrder);

        var result = await mockService.Object.CreateOrderAsync("mock-customer", new List<OrderItem>());
        result.Should().BeSameAs(expectedOrder);
    }
}
