using Coravel.Invocable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RowLogging;

public class RowLoggerCleanup<TDbContext>(
	ILogger<RowLoggerCleanup<TDbContext>> logger,
	IDbContextFactory<TDbContext> dbFactory,
	IConfiguration configuration) : IInvocable
	where TDbContext : DbContext, IRowLogDbContext
{
	private readonly ILogger<RowLoggerCleanup<TDbContext>> _logger = logger;
	private readonly IDbContextFactory<TDbContext> _dbFactory = dbFactory;
	private readonly IConfiguration _configuration = configuration;

	public Task Invoke() => ExecuteAsync();

	public async Task ExecuteAsync()
	{
		_logger.LogDebug("RowLoggerCleanup started at {Time}", DateTime.UtcNow);

		int retentionHours = _configuration.GetValue<int>("RowLogRetentionHours", 720);
		DateTime cutoffDate = DateTime.UtcNow.AddHours(-retentionHours);

		using TDbContext db = _dbFactory.CreateDbContext();

		int deletedCount = await db.RowLogs
			.Where(x => x.Timestamp < cutoffDate)
			.ExecuteDeleteAsync();

		if (deletedCount > 0)
		{
			_logger.LogInformation("RowLoggerCleanup completed: deleted {Count} records older than {CutoffDate}",
			deletedCount, cutoffDate);
		}
	}
}