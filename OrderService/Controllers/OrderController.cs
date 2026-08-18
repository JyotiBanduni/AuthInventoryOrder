using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace OrderService.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OrderDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public OrderController(OrderDbContext context,
              IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("test")]
        public async Task<IActionResult> Test()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .ToListAsync();

            return Ok(orders);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(
             CreateOrderRequest request)
        {
            // 1. Validate request
            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("Order must contain at least one item.");
            }

            if (request.Items.Any(x => x.ProductId == Guid.Empty))
            {
                return BadRequest("ProductId is required.");
            }

            if (request.Items.Any(x => x.Quantity <= 0))
            {
                return BadRequest("Quantity must be greater than 0.");
            }

            if (request.Items
                .GroupBy(x => x.ProductId)
                .Any(g => g.Count() > 1))
            {
                return BadRequest("Duplicate products are not allowed.");
            }

            // 2. Get UserId from JWT
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid user identity.");
            }

            // 3. Get InventoryService client
            var inventoryClient =
                _httpClientFactory.CreateClient("InventoryService");

            var token = Request.Headers.Authorization
                .ToString()
                .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(token))
            {
                return Unauthorized();
            }

            inventoryClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Keep track of successfully reduced stock
            var reducedItems = new List<OrderItemRequest>();

            try
            {
                // 4. Reduce stock
                foreach (var item in request.Items)
                {
                    var response = await inventoryClient.PostAsJsonAsync(
                        $"/api/products/{item.ProductId}/reduce_stock",
                        new
                        {
                            quantity = item.Quantity
                        });

                    if (!response.IsSuccessStatusCode)
                    {
                        // Restore everything already reduced
                        foreach (var reducedItem in reducedItems)
                        {
                            await inventoryClient.PostAsJsonAsync(
                                $"/api/products/{reducedItem.ProductId}/restore_stock",
                                new
                                {
                                    quantity = reducedItem.Quantity
                                });
                        }

                        return BadRequest(
                            $"Insufficient stock for product {item.ProductId}.");
                    }

                    reducedItems.Add(item);
                }

                // 5. Start Order DB transaction
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                var order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    UserId = userId,
                    OrderStatus = "Created",
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var item in request.Items)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    });
                }

                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return StatusCode(StatusCodes.Status201Created, new
                {
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    OrderStatus = order.OrderStatus,
                    CreatedAt = order.CreatedAt,
                    Items = request.Items.Select(x => new
                    {
                        ProductId = x.ProductId,
                        Quantity = x.Quantity
                    }).ToList()
                });
            }
            catch
            {
                // If Order DB transaction fails,
                // restore all stock that was reduced.
                foreach (var reducedItem in reducedItems)
                {
                    await inventoryClient.PostAsJsonAsync(
                        $"/api/products/{reducedItem.ProductId}/restore_stock",
                        new
                        {
                            quantity = reducedItem.Quantity
                        });
                }

                throw;
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .FirstOrDefaultAsync(x => x.OrderId == id);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            return Ok(new
            {
                orderId = order.OrderId,
                userId = order.UserId,
                orderStatus = order.OrderStatus,
                createdAt = order.CreatedAt,
                items = order.OrderItems.Select(x => new
                {
                    orderItemId = x.OrderItemId,
                    productId = x.ProductId,
                    quantity = x.Quantity
                })
            });
        }
    }
}
