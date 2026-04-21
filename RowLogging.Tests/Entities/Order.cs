namespace RowLogging.Tests.Entities;

public class Order : IRowLoggable
{
	public int Id { get; set; }
	public string OrderNumber { get; set; } = default!;
	public int CustomerId { get; set; }
	public string Status { get; set; } = "Pending";
	public DateTime? ShipDate { get; set; }

	public Customer Customer { get; set; } = default!;
	public ICollection<OrderLine> OrderLines { get; set; } = [];

	public string[] TrackedProperties => [nameof(Status), nameof(ShipDate)];
	public string[] ContextProperties => [nameof(OrderNumber), nameof(CustomerId)];
}
