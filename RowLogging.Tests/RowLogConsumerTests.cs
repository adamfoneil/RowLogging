using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RowLogging.Tests.Entities;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace RowLogging.Tests;

public class RowLogConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
	private DbContextOptions<AppDbContext> _options = default!;
	private OrderLineConsumer _consumer = default!;

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();

		_options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(_postgres.GetConnectionString())
			.Options;

		await using var db = new AppDbContext(_options);
		await db.Database.EnsureCreatedAsync();

		var factory = new TestDbContextFactory(_options);
		var logger = NullLogger<RowLogConsumer<AppDbContext>>.Instance;
		_consumer = new OrderLineConsumer(factory, logger, output);
	}

	public async Task DisposeAsync()
	{
		await _postgres.DisposeAsync();
	}

	[Fact]
	public async Task OrderLineConsumer_DetectsLatestChanges()
	{
		// First batch of random data
		await AddRandomOrderWithLinesAsync();

		output.WriteLine("--- First consumer run ---");
		await _consumer.ExecuteAsync();
		var markerAfterFirstRun = await GetMarkerValueAsync();
		output.WriteLine($"Marker after first run: OrderLines = {markerAfterFirstRun}");

		// Second batch of random data
		await AddRandomOrderWithLinesAsync();

		output.WriteLine("--- Second consumer run ---");
		await _consumer.ExecuteAsync();
		var markerAfterSecondRun = await GetMarkerValueAsync();
		output.WriteLine($"Marker after second run: OrderLines = {markerAfterSecondRun}");

		// Verify that the marker advanced with each run, demonstrating the consumer is tracking new changes
		Assert.True(markerAfterFirstRun > 0);
		Assert.True(markerAfterSecondRun > markerAfterFirstRun);
	}

	private async Task AddRandomOrderWithLinesAsync()
	{
		await using var db = new AppDbContext(_options);

		var suffix = Guid.NewGuid().ToString("N")[..8];
		var customer = new Customer
		{
			Name = $"Customer-{suffix}",
			Email = $"{suffix}@example.com"
		};
		db.Customers.Add(customer);
		await db.SaveChangesAsync();

		var product = new Product
		{
			Name = $"Product-{Guid.NewGuid().ToString("N")[..8]}",
			Price = Math.Round((decimal)(Random.Shared.NextDouble() * 99 + 1), 2)
		};
		db.Products.Add(product);
		await db.SaveChangesAsync();

		var order = new Order
		{
			OrderNumber = $"ORD-{Random.Shared.Next(1000, 9999)}",
			CustomerId = customer.Id,
			Status = "Pending"
		};
		db.Orders.Add(order);
		await db.SaveChangesAsync();

		int orderLineCount = Random.Shared.Next(1, 4);
		for (int i = 0; i < orderLineCount; i++)
		{
			db.OrderLines.Add(new OrderLine
			{
				OrderId = order.Id,
				ProductId = product.Id,
				Quantity = Random.Shared.Next(1, 10),
				UnitPrice = product.Price
			});
		}
		await db.SaveChangesAsync();
	}

	private async Task<long> GetMarkerValueAsync()
	{
		await using var db = new AppDbContext(_options);
		var marker = await db.RowLogMarkers.SingleOrDefaultAsync(x => x.Name == "OrderLines");
		return marker?.RowLogId ?? 0;
	}

	private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => new AppDbContext(options);
	}
}
