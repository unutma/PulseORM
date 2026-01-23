using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public interface IUserService
{
    Task<int> UserAdd(Users user);
    Task<IEnumerable<Users>> GetAllUserAsync();
    Task<Users> GetUserByIdAsync(long id);
    Task<int> BulkInsertAsync(IList<Users> users);
    Task<long> DeleteUserAsync(long id);
    Task<long> UpdateUserAsync(Users user);
}