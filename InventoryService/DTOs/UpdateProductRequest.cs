namespace InventoryService.DTOs;

public class UpdateProductRequest
{
    public string ProductName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}