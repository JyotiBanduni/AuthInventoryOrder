using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public ProductsController(InventoryDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(
    int pageNumber = 1,
    int pageSize = 10)
    {
        if (pageNumber <= 0)
        {
            return BadRequest("Page number must be greater than 0.");
        }

        if (pageSize <= 0)
        {
            return BadRequest("Page size must be greater than 0.");
        }

        var query = _context.Products
            .AsNoTracking()
            .OrderBy(x => x.ProductName);

        var totalRecords = await query.CountAsync();

        var products = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)pageSize);

        return Ok(new
        {
            pageNumber,
            pageSize,
            totalRecords,
            totalPages,
            data = products
        });
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> CreateProduct(
        CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        if (request.StockQty < 0)
        {
            return BadRequest("Stock cannot be negative.");
        }

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            ProductName = request.ProductName,
            StockQty = request.StockQty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.ProductId },
            product);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == id);

        if (product == null)
        {
            return NotFound("Product not found.");
        }

        return Ok(product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdateProduct(
    Guid id,
    UpdateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            return BadRequest("Product name is required.");
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.ProductId == id);

        if (product == null)
        {
            return NotFound("Product not found.");
        }

        product.ProductName = request.ProductName;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    [HttpPost("{id:guid}/reduce_stock")]
    public async Task<IActionResult> ReduceStock(
    Guid id,
    ReduceStockRequest request)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest("Quantity must be greater than 0.");
        }

        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE Products
        SET
            StockQty = StockQty - {request.Quantity},
            UpdatedAt = GETUTCDATE()
        WHERE
            ProductId = {id}
            AND IsActive = 1
            AND StockQty >= {request.Quantity}
        """);

        if (rowsAffected == 1)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstAsync(x => x.ProductId == id);

            return Ok(new
            {
                product.ProductId,
                product.ProductName,
                product.StockQty
            });
        }

        var existingProduct = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == id);

        if (existingProduct == null)
        {
            return NotFound("Product not found.");
        }

        if (!existingProduct.IsActive)
        {
            return BadRequest("Product is inactive.");
        }

        return BadRequest("Insufficient stock.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.ProductId == id);

        if (product == null)
        {
            return NotFound("Product not found.");
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/restore_stock")]
    [Authorize]
    public async Task<IActionResult> RestoreStock(
    Guid id,
    RestoreStockRequest request)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest("Quantity must be greater than 0.");
        }

        var rowsAffected = await _context.Database
            .ExecuteSqlInterpolatedAsync($"""
            UPDATE Products
            SET
                StockQty = StockQty + {request.Quantity},
                UpdatedAt = GETUTCDATE()
            WHERE
                ProductId = {id}
                AND IsActive = 1
            """);

        if (rowsAffected == 0)
        {
            return NotFound("Product not found.");
        }

        return Ok(new
        {
            ProductId = id,
            RestoredQuantity = request.Quantity
        });
    }
}