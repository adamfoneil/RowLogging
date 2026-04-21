# RowLogging.Tests

This project contains the xUnit integration tests and supporting infrastructure for the `RowLogging.Abstractions` library. Tests run against a real PostgreSQL instance spun up via [Testcontainers](https://dotnet.testcontainers.org/).

## Files

### Test files

| File | Description |
|------|-------------|
| [RowLogTests.cs](RowLogTests.cs) | Core integration tests that verify row-log entries are created correctly. Four facts cover adding an `Order`, modifying an `Order`'s status and ship date, adding an `OrderLine`, and modifying an `OrderLine`'s quantity. Each test saves entities through `AppDbContext.SaveChangesAsync`, then queries the `RowLogs` table and asserts that the captured `EntityState`, `RowId`, context properties, and before/after change values match expectations. |
| [RowLogConsumerTests.cs](RowLogConsumerTests.cs) | Integration tests for the `RowLogConsumer<TDbContext>` base class. The single test (`OrderLineConsumer_DetectsLatestChanges`) seeds two batches of random orders and order lines, calls `OrderLineConsumer.ExecuteAsync` after each batch, and verifies that the `RowLogMarker` advances with every run — confirming that the consumer correctly detects and tracks new changes without reprocessing already-seen rows. |

### Supporting infrastructure

| File | Description |
|------|-------------|
| [AppDbContext.cs](AppDbContext.cs) | An EF Core `DbContext` that implements `IRowLogDbContext`, making it the test stand-in for a real application database. It exposes `DbSet`s for all four test entities as well as the `RowLogs` and `RowLogMarkers` tables. The overridden `SaveChangesAsync` calls `PrepareRowLogsAsync` before saving and `SaveRowLogs` afterwards, which is the integration pattern described in the main readme. |
| [OrderLineConsumer.cs](OrderLineConsumer.cs) | A concrete subclass of `RowLogConsumer<AppDbContext>` used exclusively in `RowLogConsumerTests`. It targets the `OrderLines` table, sets `MarkerName` to `"OrderLines"`, and implements `ProcessRowLogsAsync` by writing each new row-log entry (including context and change details) to the xUnit test output. |

### Entities

These entity classes live in the `Entities/` sub-folder and model a minimal e-commerce domain used across all tests.

| File | Description |
|------|-------------|
| [Entities/Customer.cs](Entities/Customer.cs) | A simple reference entity with `Id`, `Name`, and `Email`. It does **not** implement `IRowLoggable` — changes to customers are not tracked — so it acts as a foreign-key dependency for `Order`. |
| [Entities/Order.cs](Entities/Order.cs) | Implements `IRowLoggable`. Tracks `Status` and `ShipDate` as change columns (`TrackedProperties`) and stores `OrderNumber` and `CustomerId` as context (`ContextProperties`), so those values are always present in the log even when they haven't changed. |
| [Entities/OrderLine.cs](Entities/OrderLine.cs) | Implements `IRowLoggable`. Tracks `ProductId`, `Quantity`, and `UnitPrice` as change columns and stores `OrderId` as context, linking each line-level log entry back to its parent order. |
| [Entities/Product.cs](Entities/Product.cs) | A simple reference entity with `Id`, `Name`, and `Price`. Like `Customer`, it does **not** implement `IRowLoggable` and serves purely as a foreign-key dependency for `OrderLine`. |
