using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Data;

var builder = WebApplication.CreateBuilder(args);

//Register DBContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

//Add Controlers and built-in OpenApi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    //add scalar ui

}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers(); //use controller instead of minimal api

app.Run();
