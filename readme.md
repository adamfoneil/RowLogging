# Override your DbContext SaveChanges behavior to track changes to select entity types in your applications.

This is inspired by [SQL Server change tracking](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16), adapted for PostgreSQL and EF Core. There's no Postgres-specific features used however, so it should work with any relational database provider for EF Core.

## In a nutshell
- Implement [IRowLogDbContext](./IRowLogDbContext.cs) on your DbContext. This gives you two tables `RowLog` and `RowLogMarker`.
- For each table that needs change tracking, implement [IRowLoggable](./RowLog.cs) on the corresponding entity type. This lets you define specific properties to track.
- Override your DbContext `SaveChangesAsync` method so that does three things, using methods from [DbContextExtensions](./DbContextExtensions.cs):
  - `PrepareRowLogs` scans the DbContext.ChangeTracker for `IRowLoggable` entities. This captures before/after states of modified rows in tracked entities in memory.
  - call the `base.SaveChangesAsync` method. This saves the data you were going to save originally.
  - call `SaveRowLogs` followed by another `base.SaveChangesAsync` call. This stores the before/after states of modified rows in the `RowLog` table.

## Next steps
- Implement a consumer service that inherits from [RowLogConsumer](./RowLogConsumer.cs) to do something with the row log data. This is where you can implement any custom logic that needs to happen when certain changes are detected, such as updating caches and report rollups, triggering notifications or pushing to other message queues. The base class handles the boilerplate of querying for changes and advancing the tracking marker.
- Use [RowLoggerCleanup](./RowLoggerCleanup.cs) with [Coravel](https://docs.coravel.net) to clean up old change tracking data. This is the closest you get to SQL Server's automatic cleanup of change tracking data.

## EF Core Notes
Be sure to add your own configuration for the `RowLog` and `RowLogMarker` entities in your DbContext `OnModelCreating` method. This is required to set up the relationships and indexes properly. This is not part of the base package since you may want to customize the schema or add additional properties.

<details>
<summary>But here's what I use...</summary>

```csharp
public class RowLogConfiguration : IEntityTypeConfiguration<RowLog>
{
  public void Configure(EntityTypeBuilder<RowLog> builder)
  {
    builder.ToTable("RowLogs", "logs");
    builder.Property(x => x.TableName).IsRequired().HasMaxLength(100);
    builder.HasIndex(e => new { e.TableName, e.RowId });
  }
}

public class RowLogMarkerConfiguration : IEntityTypeConfiguration<RowLogMarker>
{
  public void Configure(EntityTypeBuilder<RowLogMarker> builder)
  {
    builder.ToTable("RowLogMarkers", "logs");
    builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
    builder.HasIndex(e => e.Name).IsUnique();
  }
}
```

</details>

## Comparison: SQL Server Change Tracking vs Row Logger

| Feature | SQL Server Change Tracking | Row Logger |
|---------|---------------------------|------------|
| **Version tracking** | `CHANGE_TRACKING_CURRENT_VERSION()` returns global version number |`RowLog.Id` - auto-incrementing primary key serves as version |
| **Query changes** | `CHANGETABLE(CHANGES ...)` function | `RowLogs` DbSet with filtering by `RowId`, `TableName`, and `EntityState` |
| **Change markers** | `CHANGE_TRACKING_MIN_VALID_VERSION()` | `RowLogMarker` table with `Name` and `RowLogId` properties |
| **Enable tracking** | `ALTER TABLE ... ENABLE CHANGE_TRACKING` | Implement `IRowLoggable` interface on entity class |
| **Column selection** | Tracks primary key only, no column-level granularity | `IRowLoggable.TrackedProperties` - explicit property selection |
| **Context data** | None - only primary key tracked | `IRowLoggable.ContextProperties` - additional identifying data |
| **Prior values** | ❌ Not available - only indicates that change occurred | ✅ `RowLogData.Changes` stores old and new values in JSON |
| **Change types** | Insert, Update, Delete | `RowLog.EntityState` enum: Added, Modified, Deleted |
| **Cleanup** | Built-in with retention period | `ChangeTrackerCleanup` service + Coravel scheduler |
| **Consumer pattern** | SQL queries with `CHANGETABLE` | Abstract `ChangeTracker<TDbContext>` base class |
| **Database support** | SQL Server only | Any EF Core provider (PostgreSQL, SQLite, MySQL, etc.) |
| **Implementation** | Database engine feature, automatic | Application-level via EF Core override of `SaveChangesAsync` method |
