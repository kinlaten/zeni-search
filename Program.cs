using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

//Register DBContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Register httpClientFactory
builder.Services.AddHttpClient();

// Register Scraper Services
builder.Services.AddScoped<TheIconicScraper>();

//Add Controlers and built-in OpenApi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    //add scalar ui for testing
    app.MapScalarApiReference();

}

app.MapGet("/", () => "Hello world!");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers(); //use controller instead of minimal api

app.Run();
