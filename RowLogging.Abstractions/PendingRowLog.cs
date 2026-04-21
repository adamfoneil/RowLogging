using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RowLogging;

public class PendingRowLog
{	
	public string TableName { get; set; } = default!;
	public EntityEntry Entry { get; set; } = default!;
	public RowLogData? Data { get; set; }
	public EntityState EntityState { get; set; }
}
