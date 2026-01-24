using PulseORM.DemoEntities.Dtos;
using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public interface IUserService
{
    Task<int> UserAdd(Users user);
    Task<IEnumerable<Users>> GetAllUserAsync();
    Task<Users> GetUserByIdAsync(long id);
    Task<int> BulkInsertAsync(IList<Users> users);
    Task<int> DeleteUserAsync(int id);
    Task<int> UpdateUserAsync(Users user);
    
    Task<IEnumerable<Users>> GetUsersWithCompanyAsync();
}