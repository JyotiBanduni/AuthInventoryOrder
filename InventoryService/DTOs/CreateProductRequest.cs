namespace InventoryService.DTOs;

public class CreateProductRequest
{
    public string ProductName { get; set; } = string.Empty;

    public int StockQty { get; set; }
}