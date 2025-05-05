using server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MongoDB service
builder.Services.AddSingleton<MongoDbService>();

// Add CORS policy for both local development and production frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173", 
                    "http://localhost:5174",
                    "http://localhost:5234",
                    "http://localhost:80",
                    "https://www.ghurabafidunya.live",
                    "https://ghurabafidunya.live"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS policy with updated policy name
app.UseCors("AllowedOrigins");

app.UseAuthorization();

// Use controllers
app.MapControllers();

app.Run();
