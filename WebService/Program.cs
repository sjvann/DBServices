using DBService;
using DBService.Models.Interface;
using System.Reflection;
using WebService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "資料庫轉微服務",
            Version = "v1",
            Description = "本模組是將資料庫中資料表或檢視中的紀錄，可以透過RESTful Servicef型式提供資料服務"
        });
    var xmlFilename = "MyApiDoc.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
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
