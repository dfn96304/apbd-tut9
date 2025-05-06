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

    private async Task<bool> CheckIfExists(SqlCommand command, string tableName, string idColumnName, int id)
    {
        bool exists = false;
        command.CommandText = "SELECT COUNT(*) AS CountWithId FROM @tableName WHERE @idColumnName = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@idColumnName", idColumnName);
        var reader = await command.ExecuteReaderAsync();
        var count = reader.GetInt32(reader.GetOrdinal("CountWithId"));
        reader.Close();
        command.Parameters.Clear();
        if (count > 0) exists = true;
        else exists = false;
        return exists;
    }

    public async Task Test(TestDTO testDTO)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = connection.CreateCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = connection.BeginTransaction();
        command.Transaction = transaction as SqlTransaction;
        
        try
        {
            // Step 1
            if (!CheckIfExists(command, "Product", "IdProduct", testDTO.IdProduct).Result)
            {
                throw new Exception("Product ID not found");
            }
            
            command.Parameters.Clear();
            command.CommandText = "SELECT IdProduct, Price FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            await command.ExecuteReaderAsync();

            using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
            }

            if (!CheckIfExists(command, "Warehouse", "IdWarehouse", testDTO.IdWarehouse).Result)
            {
                throw new Exception("Warehouse ID not found");
            }

            if (!(testDTO.Amount > 0))
            {
                throw new Exception("Amount must be greater than 0");
            }

            // Step 2
            command.Parameters.Clear();
            command.CommandText = "SELECT IdOrder, CreatedAt FROM Order WHERE IdProduct = @IdProduct AND Amount = @Amount";
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            command.Parameters.AddWithValue("@Amount", testDTO.Amount);
            await command.ExecuteNonQueryAsync();
            
            int? idOrder = null;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                idOrder = reader.GetInt32(reader.GetOrdinal("IdOrder"));
                var createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"));
                if (!(createdAt < testDTO.CreatedAt))
                {
                    throw new Exception("CreatedAt of the Order is not lower than CreatedAt in the request");
                }
                if (await reader.ReadAsync()) throw new Exception();
            }
            
            // Step 3
            command.Parameters.Clear();
            command.CommandText = "SELECT COUNT(*) as CountWithId FROM Product_Warehouse WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            await command.ExecuteNonQueryAsync();
            
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                var count = reader.GetInt32(reader.GetOrdinal("CountWithId"));
                if(count > 0) throw new Exception("This order has already been completed");
            }
            
            // Step 4
            command.Parameters.Clear();
            command.CommandText = "UPDATE Order SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@FulfilledAt", DateTime.Now);
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            await command.ExecuteNonQueryAsync();
            
            // Step 5
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)";
            command.Parameters.AddWithValue("@IdWarehouse", testDTO.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", testDTO.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", idOrder.Value);
            command.Parameters.AddWithValue("@Amount", testDTO.Amount);
            //command.Parameters.AddWithValue("@Price", );
            await command.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
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