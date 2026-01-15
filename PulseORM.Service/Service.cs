namespace PulseORM.Service;

public sealed class Service
{
    private readonly IAppDb _db;
    public Service(IAppDb db) => _db = db;
    
    public async Task<List<User>> Ships()
    {
        var query = await _db.Query<User>().FilterSql(s=>s.ShipId > 1).ToListAsync();
        return query;
    }
}