using InventoryService.BackgroundServices;
using InventoryService.Infrastructure;
using InventoryService.Repositories;
using InventoryService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=CAMTU\\CAMTU;Database=SagaCommerce;User ID=sa;Password=05042003;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService.Services.InventoryService>();
builder.Services.AddHostedService<InventoryInboxProcessor>();
builder.Services.AddHostedService<InventoryOutboxDispatcher>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();