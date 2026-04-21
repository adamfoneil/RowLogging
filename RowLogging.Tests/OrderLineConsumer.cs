using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace RowLogging.Tests;

public class OrderLineConsumer(
	IDbContextFactory<AppDbContext> dbFactory,
	ILogger<RowLogConsumer<AppDbContext>> logger,
	ITestOutputHelper output) : RowLogConsumer<AppDbContext>(dbFactory, logger)
{
	protected override string MarkerName => "OrderLines";
	protected override string TableName => "OrderLines";

	protected override Task ProcessRowLogsAsync((RowLog Row, RowLogData? Data)[] newRowLogs)
	{
		foreach (var (row, data) in newRowLogs)
		{
			output.WriteLine($"  RowLog Id={row.Id}, RowId={row.RowId}, State={row.EntityState}, Timestamp={row.Timestamp:u}");
			if (data is not null)
			{
				foreach (var (key, value) in data.Context)
					output.WriteLine($"    Context: {key} = {value}");
				foreach (var (key, change) in data.Changes)
					output.WriteLine($"    Change: {key}: {change.OldValue} -> {change.NewValue}");
			}
		}
		return Task.CompletedTask;
	}
}
