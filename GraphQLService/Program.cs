using GraphQLService.Configuration;
using GraphQLService.Endpoints;
using GraphQLService.Seed;

var builder = WebApplication.CreateBuilder(args);

var connectString = builder.Configuration.GetConnectionString("default")
    ?? "Data Source=C:\\temp\\Test.db;";
var serverTypeName = builder.Configuration.GetValue<string>("ServerType:Name") ?? "Sqlite";

SeedRunner.Run(builder.Configuration);

builder.Services.AddDbGraphQL(connectString, serverTypeName, builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();

app.MapDbApiEndpoints();
app.MapGraphQL();
app.MapFallbackToFile("index.html");

app.Run();
