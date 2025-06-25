using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using server.Middleware;
using server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<QuranTextService>();
builder.Services.AddScoped<QuranDatasetDownloader>();
builder.Services.AddSingleton<WhisperRecognitionService>();
builder.Services.AddSingleton<TarteelDatasetService>();

// Register HttpClient for Tarteel service
builder.Services.AddHttpClient<TarteelDatasetService>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(
                    builder.Configuration["Jwt:Secret"] ?? 
                    "this_is_a_default_key_for_development_only_should_be_changed_in_production"
                )
            ),
            ValidateIssuer = false,
            ValidateAudience = false,
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

// Initialize services
var quranService = app.Services.GetRequiredService<QuranTextService>();
var whisperService = app.Services.GetRequiredService<WhisperRecognitionService>();
var tarteelService = app.Services.GetRequiredService<TarteelDatasetService>();

await Task.Run(async () =>
{
    await quranService.InitializeAsync();
    await whisperService.InitializeAsync();
    await tarteelService.InitializeAsync();
});

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
