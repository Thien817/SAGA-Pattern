using ShippingService.BackgroundServices;
using ShippingService.Infrastructure;
using ShippingService.Repositories;
using ShippingService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Shipping Service API",
        Version = "v1"
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=SagaCommerce;User ID=sa;Password=12345;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
builder.Services.AddScoped<IShippingRepository, ShippingRepository>();
builder.Services.AddScoped<IShippingService, global::ShippingService.Services.ShippingService>();

builder.Services.AddHostedService<ShippingInboxProcessor>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
