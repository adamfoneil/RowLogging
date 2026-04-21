using Coravel.Invocable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RowLogging;

/// <summary>
/// Queries for new RowLogs for a given table, processes them, and updates a marker to the latest processed RowLogId.
/// Always use as singleton because change tracking relies on a single global value that advances with every run.
/// </summary>
public abstract class RowLogConsumer<TDbContext>(
	IDbContextFactory<TDbContext> dbFactory,
	ILogger<RowLogConsumer<TDbContext>> logger) : IInvocable
	where TDbContext : DbContext, IRowLogDbContext
{
	protected readonly IDbContextFactory<TDbContext> DbFactory = dbFactory;
	protected readonly ILogger<RowLogConsumer<TDbContext>> Logger = logger;

	/// <summary>
	/// Multiple handlers can exist, so each needs its own marker name.
	/// They might process rows from the same table, but have different logic.
	/// </summary>
	protected abstract string MarkerName { get; }
	/// <summary>
	/// table name in logs.RowLogs. A RowHandler is assumed to handle one table.
	/// </summary>
	protected abstract string TableName { get; }

	private async Task<RowLogMarker> GetMarkerAsync(TDbContext db) =>
		await db.RowLogMarkers.SingleOrDefaultAsync(x => x.Name == MarkerName) ?? new() { Name = MarkerName };

	/// <summary>
	/// your processing runs here
	/// </summary>
	protected abstract Task ProcessRowLogsAsync((RowLog Row, RowLogData? Data)[] newRowLogs);

	private async Task ExecuteInternalAsync(string trigger)
	{
		using TDbContext db = DbFactory.CreateDbContext();

		_ = Logger.BeginScope("{Type} {Trigger} trigger RowLogHandler for marker {MarkerName}, table {TableName}", this.GetType().Name, trigger, MarkerName, TableName);

		var marker = await GetMarkerAsync(db);

		var rowLogs = await GetNewRowLogsAsync(db, marker.RowLogId);

		if (!rowLogs.Any())
		{
			Logger.LogDebug("No new row logs from {RowLogId}", marker.RowLogId);
			return;
		}

		// on successful execution, update marker to this value
		long maxRowLogId = rowLogs.Max(tuple => tuple.Log.Id);

		try
		{
			Logger.LogInformation("Starting to process {Count} RowLogs from Id {Id}", rowLogs.Length, marker.RowLogId);
			await ProcessRowLogsAsync(rowLogs);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error processing RowLogs");
			throw;
		}

		try
		{
			Logger.LogDebug("Updating RowLogMarker to RowLogId {RowLogId}", maxRowLogId);
			marker.RowLogId = maxRowLogId;
			marker.Timestamp = DateTime.UtcNow;
			db.RowLogMarkers.Update(marker);
			await db.SaveChangesAsync();
		}
		catch (Exception exc)
		{
			Logger.LogError(exc, "Error updating RowLogMarker to RowLogId {RowLogId}", marker.RowLogId);
			throw;
		}
	}

	public async Task ExecuteAsync() => await ExecuteInternalAsync("Manual");
	
	private async Task<(RowLog Log, RowLogData? Data)[]> GetNewRowLogsAsync(TDbContext db, long startingRowLogId)
	{
		var rowLogs = await db.RowLogs.AsNoTracking().Where(x => x.TableName == TableName && x.Id > startingRowLogId)
			.OrderBy(x => x.Id)
			.ToArrayAsync();

		return [.. rowLogs.Select(row => (row, row.Data is not null ? JsonSerializer.Deserialize<RowLogData>(row.Data) : null))];
	}

	public async Task Invoke() => await ExecuteInternalAsync("Scheduled");
}