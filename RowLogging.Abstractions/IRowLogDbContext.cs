using Microsoft.EntityFrameworkCore;

namespace RowLogging;

public interface IRowLogDbContext
{
	/// <summary>
	/// similar to SQL Server CHANGE_TABLE function results, stores the actual changes in a JSON structure along with some context properties
	/// </summary>
	public DbSet<RowLog> RowLogs { get; set; }
	/// <summary>
	/// tracks latest change so that we can efficiently query for changes since a given point in time without needing to scan the entire RowLogs table
	/// </summary>
	public DbSet<RowLogMarker> RowLogMarkers { get; set; }
}
