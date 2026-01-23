using PulseORM.Core;
using PulseORM.Core.Sql;
using PulseORM.DemoDataLayer;
using PulseORM.DemoService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IDbConnectionFactory>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
             ?? throw new InvalidOperationException("Missing connection string: ConnectionStrings:Default");
    return new NpgsqlConnectionFactory(cs);
});

builder.Services.AddScoped<ISqlDialect, PostgresDialect>();

builder.Services.AddScoped<PulseLiteDb>();
builder.Services.AddScoped<IAppDb, AppDb>();

builder.Services.AddScoped<IPulseDbContext, PulseDbContext>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();