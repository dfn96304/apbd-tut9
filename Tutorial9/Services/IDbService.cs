using Tutorial9.Model.DTOs;

namespace Tutorial9.Services;

public interface IDbService
{
    public Task<int> Test(TestDTO testDTO);
    //public Task<int> TestStored(TestDTO testDTO);
}