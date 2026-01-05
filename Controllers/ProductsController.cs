using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KeyClockWebAPI.Models;

namespace KeyClockWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ILogger<ProductsController> _logger;
    private static readonly List<Product> _products = new();

    public ProductsController(ILogger<ProductsController> logger)
    {
        _logger = logger;

        // Initialize with sample data if empty
        if (!_products.Any())
        {
            _products.AddRange(new List<Product>
            {
                new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, Category = "Electronics" },
                new Product { Id = 2, Name = "Smartphone", Description = "Latest smartphone", Price = 699.99m, Category = "Electronics" },
                new Product { Id = 3, Name = "Coffee Maker", Description = "Automatic coffee maker", Price = 89.99m, Category = "Appliances" },
                new Product { Id = 4, Name = "Book", Description = "Programming guide", Price = 29.99m, Category = "Books" }
            });
        }
    }

    [HttpGet]
    [AllowAnonymous] // Public endpoint - anyone can view products
    public ActionResult<ApiResponse<List<Product>>> GetAllProducts()
    {
        return Ok(new ApiResponse<List<Product>>
        {
            Success = true,
            Message = "Products retrieved successfully",
            Data = _products
        });
    }

    [HttpGet("{id}")]
    [AllowAnonymous] // Public endpoint - anyone can view a specific product
    public ActionResult<ApiResponse<Product>> GetProduct(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new ApiResponse<Product>
            {
                Success = false,
                Message = "Product not found"
            });
        }

        return Ok(new ApiResponse<Product>
        {
            Success = true,
            Message = "Product retrieved successfully",
            Data = product
        });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")] // Only admins can create products
    public ActionResult<ApiResponse<Product>> CreateProduct([FromBody] Product product)
    {
        if (product == null)
        {
            return BadRequest(new ApiResponse<Product>
            {
                Success = false,
                Message = "Invalid product data"
            });
        }

        product.Id = _products.Any() ? _products.Max(p => p.Id) + 1 : 1;
        product.CreatedAt = DateTime.UtcNow;
        _products.Add(product);

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, new ApiResponse<Product>
        {
            Success = true,
            Message = "Product created successfully",
            Data = product
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")] // Only admins can update products
    public ActionResult<ApiResponse<Product>> UpdateProduct(int id, [FromBody] Product updatedProduct)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new ApiResponse<Product>
            {
                Success = false,
                Message = "Product not found"
            });
        }

        product.Name = updatedProduct.Name;
        product.Description = updatedProduct.Description;
        product.Price = updatedProduct.Price;
        product.Category = updatedProduct.Category;
        product.IsAvailable = updatedProduct.IsAvailable;

        return Ok(new ApiResponse<Product>
        {
            Success = true,
            Message = "Product updated successfully",
            Data = product
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")] // Only admins can delete products
    public ActionResult<ApiResponse<bool>> DeleteProduct(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new ApiResponse<Product>
            {
                Success = false,
                Message = "Product not found"
            });
        }

        _products.Remove(product);

        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "Product deleted successfully",
            Data = true
        });
    }

    [HttpGet("category/{category}")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<List<Product>>> GetProductsByCategory(string category)
    {
        var products = _products.Where(p => p.Category.ToLower() == category.ToLower()).ToList();

        return Ok(new ApiResponse<List<Product>>
        {
            Success = true,
            Message = $"Products in category '{category}' retrieved successfully",
            Data = products
        });
    }
}