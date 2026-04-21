using Microsoft.EntityFrameworkCore;
using RowLogging.Tests.Entities;
using Testcontainers.PostgreSql;

namespace RowLogging.Tests;

public class RowLogTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
	private AppDbContext _db = default!;

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(_postgres.GetConnectionString())
			.Options;

		_db = new AppDbContext(options);
		await _db.Database.EnsureCreatedAsync();
	}

	public async Task DisposeAsync()
	{
		await _db.DisposeAsync();
		await _postgres.DisposeAsync();
	}

	[Fact]
	public async Task AddOrder_CreatesRowLog()
	{
		var customer = new Customer { Name = "Alice", Email = "alice@example.com" };
		_db.Customers.Add(customer);
		await _db.SaveChangesAsync();

		var order = new Order
		{
			OrderNumber = "ORD-001",
			CustomerId = customer.Id,
			Status = "Pending"
		};
		_db.Orders.Add(order);
		await _db.SaveChangesAsync();

		var log = await _db.RowLogs.SingleAsync(x => x.TableName == "Orders");
		Assert.Equal(order.Id, log.RowId);
		Assert.Equal(EntityState.Added, log.EntityState);

		var data = System.Text.Json.JsonSerializer.Deserialize<RowLogData>(log.Data!);
		Assert.NotNull(data);
		Assert.Equal("ORD-001", data.Context["OrderNumber"]);
		Assert.Equal(customer.Id.ToString(), data.Context["CustomerId"]);
		Assert.Equal("Pending", data.Changes["Status"].NewValue);
	}

	[Fact]
	public async Task ModifyOrderStatus_CreatesRowLog()
	{
		var customer = new Customer { Name = "Bob", Email = "bob@example.com" };
		_db.Customers.Add(customer);
		await _db.SaveChangesAsync();

		var order = new Order
		{
			OrderNumber = "ORD-002",
			CustomerId = customer.Id,
			Status = "Pending"
		};
		_db.Orders.Add(order);
		await _db.SaveChangesAsync();

		order.Status = "Shipped";
		order.ShipDate = new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
		await _db.SaveChangesAsync();

		var logs = await _db.RowLogs
			.Where(x => x.TableName == "Orders" && x.EntityState == EntityState.Modified)
			.ToListAsync();

		Assert.Single(logs);
		var log = logs[0];
		Assert.Equal(order.Id, log.RowId);

		var data = System.Text.Json.JsonSerializer.Deserialize<RowLogData>(log.Data!);
		Assert.NotNull(data);
		Assert.Equal("Pending", data.Changes["Status"].OldValue);
		Assert.Equal("Shipped", data.Changes["Status"].NewValue);
		Assert.NotNull(data.Changes["ShipDate"].NewValue);
	}

	[Fact]
	public async Task AddOrderLine_CreatesRowLog()
	{
		var customer = new Customer { Name = "Carol", Email = "carol@example.com" };
		var product = new Product { Name = "Widget", Price = 9.99m };
		_db.Customers.Add(customer);
		_db.Products.Add(product);
		await _db.SaveChangesAsync();

		var order = new Order
		{
			OrderNumber = "ORD-003",
			CustomerId = customer.Id,
			Status = "Pending"
		};
		_db.Orders.Add(order);
		await _db.SaveChangesAsync();

		var orderLine = new OrderLine
		{
			OrderId = order.Id,
			ProductId = product.Id,
			Quantity = 2,
			UnitPrice = 9.99m
		};
		_db.OrderLines.Add(orderLine);
		await _db.SaveChangesAsync();

		var log = await _db.RowLogs
			.SingleAsync(x => x.TableName == "OrderLines" && x.EntityState == EntityState.Added);
		Assert.Equal(orderLine.Id, log.RowId);

		var data = System.Text.Json.JsonSerializer.Deserialize<RowLogData>(log.Data!);
		Assert.NotNull(data);
		Assert.Equal(order.Id.ToString(), data.Context["OrderId"]);
		Assert.Equal(product.Id.ToString(), data.Changes["ProductId"].NewValue);
		Assert.Equal("2", data.Changes["Quantity"].NewValue);
		Assert.Equal("9.99", data.Changes["UnitPrice"].NewValue);
	}

	[Fact]
	public async Task ModifyOrderLine_CreatesRowLog()
	{
		var customer = new Customer { Name = "Dave", Email = "dave@example.com" };
		var product = new Product { Name = "Gadget", Price = 19.99m };
		_db.Customers.Add(customer);
		_db.Products.Add(product);
		await _db.SaveChangesAsync();

		var order = new Order
		{
			OrderNumber = "ORD-004",
			CustomerId = customer.Id,
			Status = "Pending"
		};
		_db.Orders.Add(order);
		await _db.SaveChangesAsync();

		var orderLine = new OrderLine
		{
			OrderId = order.Id,
			ProductId = product.Id,
			Quantity = 1,
			UnitPrice = 19.99m
		};
		_db.OrderLines.Add(orderLine);
		await _db.SaveChangesAsync();

		orderLine.Quantity = 3;
		await _db.SaveChangesAsync();

		var log = await _db.RowLogs
			.SingleAsync(x => x.TableName == "OrderLines" && x.EntityState == EntityState.Modified);
		Assert.Equal(orderLine.Id, log.RowId);

		var data = System.Text.Json.JsonSerializer.Deserialize<RowLogData>(log.Data!);
		Assert.NotNull(data);
		Assert.Equal("1", data.Changes["Quantity"].OldValue);
		Assert.Equal("3", data.Changes["Quantity"].NewValue);
	}
}
