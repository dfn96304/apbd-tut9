using Microsoft.AspNetCore.Mvc;
using Tutorial9.Model.DTOs;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WarehouseController : ControllerBase
{
    private readonly IDbService _dbService;
    
    public WarehouseController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test([FromBody] TestDTO testDTO)
    {
        
    }

    [HttpGet("storedTest")]
    public async Task<IActionResult> StoredTest()
    {
        
    }
}