using DBService;
using DBService.Models.Interface;
using WebService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var connectString = builder.Configuration.GetConnectionString("default")??"Data Source=c://temp//Test.db;Version=3;";
var serverTypeName = builder.Configuration.GetValue<string>("ServerType:Name")??"Sqlite";
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDbWebService(connectString, serverTypeName);

app.Run();
