using PulseORM.DemoDataLayer;
using PulseORM.DemoDataLayer.Tables;
using PulseORM.DemoService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IPulseDbContext, PulseDbContext>();
builder.Services.AddScoped<IAppDb, AppDb>();
builder.Services.AddScoped<ICompanyService, CompanyService>();

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