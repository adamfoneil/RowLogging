using Microsoft.EntityFrameworkCore;

namespace RowLogging;

/// <summary>
/// implement on entity classes to indicate which properties should be logged in RowLog
/// </summary>
public interface IRowLoggable
{
	/// <summary>
	/// what's relevant to know about the row where the change happened (always recorded)
	/// </summary>
	string[] ContextProperties { get; }
	/// <summary>
	/// what properties are useful to know the prior and new values for (recorded only if changed)
	/// </summary>
	string[] TrackedProperties { get; }
}

public class RowLogData
{
	public Dictionary<string, string> Context { get; set; } = [];
	public Dictionary<string, Change> Changes { get; set; } = [];

	public class Change
	{
		public string? OldValue { get; set; }
		public string? NewValue { get; set; }
	}

	public string? GetValue(string propertyName, Func<Change, string?> selector)
	{
		if (Changes.TryGetValue(propertyName, out Change? change)) return selector(change);
		if (Context.TryGetValue(propertyName, out string? contextValue)) return contextValue;
		return null;
	}

	public T? GetValueAs<T>(string propertyName, Func<Change, string?> selector, Func<string, T> convert)
	{
		string? value = GetValue(propertyName, selector);
		return value != null ? convert(value) : default;
	}
}

/// <summary>
/// behaves sort of like SQL Server change tracking
/// </summary>
public class RowLog
{
	public long Id { get; set; }
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	public string TableName { get; set; } = default!;
	public int RowId { get; set; }
	/// <summary>
	/// json structure of modified data
	/// </summary>
	public string? Data { get; set; }
	public EntityState EntityState { get; set; }
}
