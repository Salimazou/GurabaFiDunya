using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using server.Middleware;
using server.Services;
using server.Migration;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Check for migration command
if (args.Length > 0 && args[0] == "migrate-users")
{
    // Build minimal services for migration
    builder.Services.AddSingleton<MongoDbService>();
    var migrationApp = builder.Build();
    
    var mongoService = migrationApp.Services.GetRequiredService<MongoDbService>();
    var migration = new UserMigration(mongoService);
    
    await migration.MigrateUsersAsync();
    return;
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();

// Register background services
builder.Services.AddHostedService<ReminderBackgroundService>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(
                    builder.Configuration["Jwt:Key"] ?? 
                    throw new InvalidOperationException("JWT Key not configured")
                )
            ),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

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

// Enable authentication
app.UseAuthentication();
app.UseAuthorization();

// Add custom admin authentication middleware
app.UseAdminAuth();

// Use controllers
app.MapControllers();

app.Run();
