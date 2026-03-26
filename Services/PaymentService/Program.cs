using PaymentService.BackgroundServices;
using PaymentService.Infrastructure;
using PaymentService.Repositories;
using PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Payment Service API",
        Version = "v1"
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=CAMTU\\CAMTU;Database=SagaCommerce;User ID=sa;Password=05042003;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, global::PaymentService.Services.PaymentService>();
builder.Services.AddHostedService<PaymentInboxProcessor>();
builder.Services.AddHostedService<PaymentOutboxDispatcher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();