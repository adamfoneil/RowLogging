using Microsoft.EntityFrameworkCore;
using RowLogging.Tests.Entities;

namespace RowLogging.Tests;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IRowLogDbContext
{
	public DbSet<Customer> Customers => Set<Customer>();
	public DbSet<Product> Products => Set<Product>();
	public DbSet<Order> Orders => Set<Order>();
	public DbSet<OrderLine> OrderLines => Set<OrderLine>();
	public DbSet<RowLog> RowLogs { get; set; } = default!;
	public DbSet<RowLogMarker> RowLogMarkers { get; set; } = default!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<RowLog>(builder =>
		{
			builder.ToTable("RowLogs", "logs");
			builder.Property(x => x.TableName).IsRequired().HasMaxLength(100);
			builder.HasIndex(e => new { e.TableName, e.RowId });
		});

		modelBuilder.Entity<RowLogMarker>(builder =>
		{
			builder.ToTable("RowLogMarkers", "logs");
			builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
			builder.HasIndex(e => e.Name).IsUnique();
		});
	}

	public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		var pendingLogs = await this.PrepareRowLogsAsync();
		var result = await base.SaveChangesAsync(cancellationToken);
		this.SaveRowLogs(pendingLogs);
		await base.SaveChangesAsync(cancellationToken);
		return result;
	}
}
