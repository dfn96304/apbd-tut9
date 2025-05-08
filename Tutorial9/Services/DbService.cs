using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Tutorial9.Model.DTOs;

namespace Tutorial9.Services;

public class DbService : IDbService
{
    private readonly IConfiguration _configuration;

    public DbService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private async Task<bool> CheckIfWarehouseExists(SqlCommand command, int id)
    {
        bool exists = false;
        command.CommandText = "SELECT COUNT(*) AS CountWithId FROM Warehouse WHERE IdWarehouse = @id";
        command.Parameters.AddWithValue("@id", id);
        var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var count = reader.GetInt32(reader.GetOrdinal("CountWithId"));
        reader.Close();
        command.Parameters.Clear();
        if (count > 0) exists = true;
        else exists = false;
        return exists;
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public async Task<int> Test(TestDTO testDTO)
    {
        int ret = -1;
        
        //Console.WriteLine("Connection string: "+_configuration.GetConnectionString("Default"));
        
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = connection.BeginTransaction();
        command.Transaction = transaction as SqlTransaction;
        
        try
        {
            // Step 1
            command.Parameters.Clear();
            command.CommandText = "SELECT IdProduct, Price FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            // Execute *only once*, either command.ExecuteNonQueryAsync() OR command.ExecuteReaderAsync()
            //await command.ExecuteNonQueryAsync();

            decimal price;
            
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    throw new NotFoundException("Product ID not found");
                }
                price = reader.GetDecimal(reader.GetOrdinal("Price"));
                if (await reader.ReadAsync()) throw new Exception();
                reader.Close();
            }
            
            decimal priceAmount = price * testDTO.Amount;

            if (!CheckIfWarehouseExists(command, testDTO.IdWarehouse).Result)
            {
                throw new NotFoundException("Warehouse ID not found");
            }

            if (!(testDTO.Amount > 0))
            {
                throw new ArgumentException("Amount must be greater than 0");
            }

            // Step 2
            command.Parameters.Clear();
            // !!! Order needs to be quoted
            command.CommandText = "SELECT IdOrder, CreatedAt FROM \"Order\" WHERE IdProduct = @IdProduct AND Amount = @Amount";
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            command.Parameters.AddWithValue("@Amount", testDTO.Amount);
            //await command.ExecuteNonQueryAsync();
            
            int? idOrder = null;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    throw new NotFoundException("Corresponding order not found");
                }
                idOrder = reader.GetInt32(reader.GetOrdinal("IdOrder"));
                var createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"));
                if (!(createdAt < testDTO.CreatedAt))
                {
                    throw new ArgumentException($"CreatedAt of the Order ({createdAt}) is not lower than CreatedAt in the request ({testDTO.CreatedAt})");
                }
                if (await reader.ReadAsync()) throw new Exception();
                reader.Close();
            }
            
            // Step 3
            command.Parameters.Clear();
            command.CommandText = "SELECT COUNT(*) as CountWithId FROM Product_Warehouse WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            //await command.ExecuteNonQueryAsync();
            
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                var count = reader.GetInt32(reader.GetOrdinal("CountWithId"));
                if(count > 0) throw new Exception("This order has already been completed");
                reader.Close();
            }
            
            // Step 4
            command.Parameters.Clear();
            // Order quoted
            command.CommandText = "UPDATE \"Order\" SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@FulfilledAt", DateTime.Now);
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            await command.ExecuteNonQueryAsync();
            
            // Step 5+6
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) OUTPUT INSERTED.IdProductWarehouse VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)";
            command.Parameters.AddWithValue("@IdWarehouse", testDTO.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            command.Parameters.AddWithValue("@Amount", testDTO.Amount);
            command.Parameters.AddWithValue("@Price", priceAmount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            //await command.ExecuteNonQueryAsync();
            
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                ret = reader.GetInt32(reader.GetOrdinal("IdProductWarehouse"));
                reader.Close();
            }
            
            command.Parameters.Clear();
            
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            //Console.WriteLine("Rollback: "+e.Message);
            await transaction.RollbackAsync();
            throw;
        }

        return ret;
    }

    public async Task<int> TestStored(TestDTO testDTO)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();
        
        command.Connection = connection;
        await connection.OpenAsync();
        
        DbTransaction transaction = connection.BeginTransaction();
        command.Transaction = transaction as SqlTransaction;

        try
        {
            // Run stored procedure
            command.CommandText = "EXEC AddProductToWarehouse @IdProduct, @IdWarehouse, @Amount, @CreatedAt;";
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            command.Parameters.AddWithValue("@IdWarehouse", testDTO.IdWarehouse);
            command.Parameters.AddWithValue("@Amount", testDTO.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            var result = await command.ExecuteScalarAsync();
            int productWarehouseId = Convert.ToInt32(result);

            return productWarehouseId;
        }
        catch (Exception e)
        {
            Console.WriteLine("Rollback: "+e.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    public async Task DoSomethingAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        // BEGIN TRANSACTION
        try
        {
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 1);
            command.Parameters.AddWithValue("@Name", "Animal1");

            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 2);
            command.Parameters.AddWithValue("@Name", "Animal2");

            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
        // END TRANSACTION
    }

    public async Task ProcedureAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        command.CommandText = "NazwaProcedury";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@Id", 2);

        await command.ExecuteNonQueryAsync();
    }
}