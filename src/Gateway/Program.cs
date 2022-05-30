using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOcelot();
  builder.Services.AddCors(options =>
             {
                 options.AddPolicy("CorsPolicy",
                     builder => builder
                      //.AllowAnyOrigin()
                      .WithOrigins(
                             "http://localhost:8080"));
             });

var app = builder.Build();
app.UseCors("CorsPolicy");

app.UseOcelot().Wait();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
