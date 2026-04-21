namespace RowLogging;

public class RowLogMarker
{
	public long Id { get; set; }
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	/// <summary>
	/// can be a table name or any identifier used by a ChangeTracker class
	/// </summary>
	public string Name { get; set; } = default!;
	/// <summary>
	/// last queried RowLog.Id for this table, causes future queries to get only newer rows
	/// </summary>
	public long RowLogId { get; set; }

	public string DisplayText => $"{Name} = {RowLogId}";
}
