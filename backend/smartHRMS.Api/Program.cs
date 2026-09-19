using Microsoft.EntityFrameworkCore;
using smartHRMS.Api.Data;


var builder = WebApplication.CreateBuilder(args);

//add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();


//database connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

//http request pipeline


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();