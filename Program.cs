using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using server.Services;
using System.Text;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register our simple services
builder.Services.AddSingleton<SimpleDbService>();
builder.Services.AddSingleton<SimpleJwtService>();
builder.Services.AddSingleton<PushNotificationService>();
builder.Services.AddScoped<ReminderBackgroundService>();

// Configure Hangfire with MongoDB
var baseConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";

// For Hangfire, use a explicit database name format
var hangfireConnectionString = "mongodb+srv://salimazazouaoui:U6jYsyqbnqQpcY9Z@cluster0.lswrffy.mongodb.net/GhareebDB?retryWrites=true&w=majority&appName=Cluster0";

var migrationOptions = new MongoMigrationOptions
{
    MigrationStrategy = new MigrateMongoMigrationStrategy(),
    BackupStrategy = new CollectionMongoBackupStrategy()
};

builder.Services.AddHangfire(config =>
{
    config.UseMongoStorage(hangfireConnectionString, new MongoStorageOptions
    {
        MigrationOptions = migrationOptions,
        Prefix = "hangfire",
        CheckConnection = true
    });
});
builder.Services.AddHangfireServer();

// Add simple JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your_super_secret_key_that_should_be_in_config";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Add simple CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.MapControllers();

// Schedule recurring background jobs
ReminderBackgroundService.ScheduleRecurringJobs();

app.Run();

// Custom authorization filter for Hangfire dashboard
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In development, allow all access
        // In production, you should implement proper authorization
        return true; // TODO: Implement proper admin authorization
    }
} 