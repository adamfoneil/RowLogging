using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace RowLogging;

public static class DbContextExtensions
{
	/// <summary>
	/// determines which entities are being tracked that implement IRowLoggable and prepares a list of PendingRowLog objects with the necessary data to log changes after SaveChanges is called. 
	/// This method captures both context properties and tracked property changes, including old and new values for modified entities, new values for added entities, 
	/// and old values for deleted entities. It also handles cases where there may be no changes or context to log by allowing the Data property to be null.
	/// </summary>
	public static async Task<List<PendingRowLog>> PrepareRowLogsAsync(this DbContext dbContext)
	{
		var pendingLogs = new List<PendingRowLog>();

		var loggableEntries = dbContext.ChangeTracker.Entries()
			.Where(e => e.Entity is IRowLoggable &&
						(e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
			.ToArray();

		foreach (var entry in loggableEntries)
		{
			if (entry.Entity is not IRowLoggable loggable) continue;			

			var rowLogData = new RowLogData();

			// For Modified or Deleted entities, get the actual database values to compare against
			PropertyValues? databaseValues = null;
			if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
			{
				// get database values if there are any tracked or context properties to log, otherwise skip the database call
				databaseValues = (loggable.TrackedProperties.Any() || loggable.ContextProperties.Any()) ? await entry.GetDatabaseValuesAsync() : null;
			}

			// Capture context properties
			bool isDeleted = entry.State == EntityState.Deleted;

			foreach (var propName in loggable.ContextProperties)
			{
				var prop = entry.Property(propName);
				if (prop != null)
				{
					// For deleted entities, use database values to ensure accurate logging before deletion
					string? value = isDeleted
						? databaseValues?[propName]?.ToString()
						: prop.CurrentValue?.ToString();
					rowLogData.Context[propName] = value ?? string.Empty;
				}
			}

			// Capture tracked properties (prior and new values for Modified, new values only for Added, old values only for Deleted)
			foreach (var propName in loggable.TrackedProperties)
			{
				var prop = entry.Property(propName);
				if (prop != null)
				{
					// For Modified entities, capture both old and new values if the property changed
					if (entry.State == EntityState.Modified && prop.IsModified)
					{
						var oldValue = databaseValues?[propName]?.ToString();
						var newValue = prop.CurrentValue?.ToString();

						// Only log if there's an actual change
						if (oldValue != newValue)
						{
							rowLogData.Changes[propName] = new RowLogData.Change
							{
								OldValue = oldValue,
								NewValue = newValue
							};
						}
					}
					// For Added entities, capture only the new value
					else if (entry.State == EntityState.Added)
					{
						rowLogData.Changes[propName] = new RowLogData.Change
						{
							OldValue = null,
							NewValue = prop.CurrentValue?.ToString()
						};
					}
					// For Deleted entities, capture the database value as the old value, and null as new value
					else if (entry.State == EntityState.Deleted)
					{
						var oldValue = databaseValues?[propName]?.ToString();
						rowLogData.Changes[propName] = new RowLogData.Change
						{
							OldValue = oldValue,
							NewValue = null
						};
					}
				}
			}
			
			if (rowLogData is { Changes.Count: 0, Context.Count: 0 })
			{
				// No changes or context to log, so we can set it to null
				rowLogData = null; 
			}

			pendingLogs.Add(new PendingRowLog
			{					
				TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
				Entry = entry,
				Data = rowLogData,
				EntityState = entry.State
			});
		}

		return pendingLogs;
	}

	/// <summary>
	/// Saves pending row logs as RowLog entities in the database context.
	/// </summary>
	public static void SaveRowLogs(this DbContext dbContext, List<PendingRowLog> pendingLogs)
	{
		foreach (var pending in pendingLogs)
		{
			var rowLog = new RowLog
			{
				TableName = pending.TableName,
				RowId = pending.Entry.Property("Id").CurrentValue is int id ? id : throw new Exception("Entity must have int Id property"),
				Data = pending.Data is not null ? JsonSerializer.Serialize(pending.Data) : null,
				Timestamp = DateTime.UtcNow,
				EntityState = pending.EntityState
			};

			dbContext.Set<RowLog>().Add(rowLog);
		}
	}
}
