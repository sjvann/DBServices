using WebService;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// .NET 10: use the built-in OpenAPI endpoints (Swagger UI is typically provided by Scalar).
builder.Services.AddOpenApi();
var connectString = builder.Configuration.GetConnectionString("default")??"Data Source=c://temp//Test.db;";
var serverTypeName = builder.Configuration.GetValue<string>("ServerType:Name")??"Sqlite";
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Scalar UI (default route: /scalar)
    app.MapScalarApiReference();
}


app.UseHttpsRedirection();
app.UseDbWebService(connectString, serverTypeName);

app.Run();
