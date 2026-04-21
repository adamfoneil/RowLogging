namespace RowLogging.Tests.Entities;

public class OrderLine : IRowLoggable
{
	public int Id { get; set; }
	public int OrderId { get; set; }
	public int ProductId { get; set; }
	public int Quantity { get; set; }
	public decimal UnitPrice { get; set; }

	public Order Order { get; set; } = default!;
	public Product Product { get; set; } = default!;

	public string[] TrackedProperties => [nameof(ProductId), nameof(Quantity), nameof(UnitPrice)];
	public string[] ContextProperties => [nameof(OrderId)];
}
