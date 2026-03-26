using InventoryService.Infrastructure;
using InventoryService.Repositories;
using InventoryService.Services;

// Ensure the correct class is referenced instead of the namespace


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=SagaCommerce;User ID=sa;Password=12345;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService.Services.InventoryService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();