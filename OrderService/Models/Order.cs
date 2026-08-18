namespace OrderService.Models;

public class Order
{
    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public string OrderStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();
}