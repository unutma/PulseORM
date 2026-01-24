using PulseORM.Core;
using PulseORM.DemoEntities.Dtos;
using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public class UserService : IUserService
{
    
    private readonly IAppDb _appDb;
    
    public UserService(IAppDb appDb)
    {
        _appDb = appDb;
    }
    public async Task<int> UserAdd(Users user)
    {
        try
        {
            user.FirstName = await this.Capitalize(user.FirstName);
            user.LastName = await this.Capitalize(user.LastName);
            var addUser = await _appDb.InsertAsync(user);
            return addUser;
        }
        catch (Exception ex)
        {
           throw new Exception(ex.Message);
        }
    }

    public async Task<IEnumerable<Users>> GetAllUserAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<Users> GetUserByIdAsync(long id)
    {
        var user = await _appDb.GetByIdAsync<Users>(id);
        return user;
    }

    public async Task<int> BulkInsertAsync(IList<Users> users)
    {
        foreach (var user in users)
        {
            user.FirstName = await this.Capitalize(user.FirstName);
            user.LastName = await this.Capitalize(user.LastName);
        }
        var addUsers = await _appDb.BulkInsertAsync(users);
        return addUsers;
    }

    public async Task<int> DeleteUserAsync(int id)
    {
        throw new NotImplementedException();
    }

    public async Task<int> UpdateUserAsync(Users user)
    {
        var member = await _appDb.GetByIdAsync<Users>(user.UserId);
        if (member == null)
            throw new Exception("User not found");
        member.CompanyId = user.CompanyId;
        member.FirstName = user.FirstName;
        member.LastName = user.LastName;
        member.Title = user.Title;
        await _appDb.UpdateAsync(member);
        return member.UserId;
        
    }

    public async Task<IEnumerable<Users>> GetUsersWithCompanyAsync()
    {
        var member = await _appDb.QueryJoin<Users>().FilterSql(s => s.CompanyId == 1)
            .IncludeOne(x => x.Company, x => x.CompanyId, c => c.CompanyId, JoinType.Inner)
            .SortBy(x => x.CompanyId, true).Pagination(1,1).ToListAsync();
        return member.Items;
    }

    private async Task <string> Capitalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;
        input = input.ToLower();
        return char.ToUpper(input[0]) + input.Substring(1);
    }

}