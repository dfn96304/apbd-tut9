using Microsoft.AspNetCore.Mvc;
using Tutorial9.Model.DTOs;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

/*
 {
    "IdProduct": 1,
    "IdWarehouse": 2,
    "Amount": 20,
    "CreatedAt": "2012-04-23T18:25:43.511Z"
 }
 */

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
        Task<int> task;
        try
        {
            task = _dbService.Test(testDTO);
        }
        catch (DbService.NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException e1)
        {
            return BadRequest(e1.Message);
        }

        return Ok(task.Result);
    }

    [HttpGet("testStored")]
    public async Task<IActionResult> TestStored([FromBody] TestDTO testDTO)
    {
        Task<int> task;
        try
        {
            task = _dbService.TestStored(testDTO);
        }
        catch (DbService.NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException e1)
        {
            return BadRequest(e1.Message);
        }
        
        return Ok(task.Result);
    }
}