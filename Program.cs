using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GurabaFiDunya.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

var builder = WebApplication.CreateBuilder(args);

// Configure MongoDB BSON serialization for DateOnly and TimeOnly
BsonSerializer.RegisterSerializer(new DateOnlySerializer());
BsonSerializer.RegisterSerializer(new TimeOnlySerializer());

// Add services to the container
builder.Services.AddControllers();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add MongoDB services
builder.Services.AddSingleton<DatabaseService>();

// Add JWT services
builder.Services.AddSingleton<JwtService>();

// Add HttpClient for external API calls
builder.Services.AddHttpClient();

// Configure JWT Authentication
var jwtKey = builder.Configuration["JWT:Key"] ?? throw new InvalidOperationException("JWT Key not found");
var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found");
var jwtAudience = builder.Configuration["JWT:Audience"] ?? throw new InvalidOperationException("JWT Audience not found");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add authorization
builder.Services.AddAuthorization();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Islam App API",
        Version = "v1",
        Description = "A comprehensive API for the Islam Reminder App with Quran audio streaming and user management."
    });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Islam App API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the root
    });
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Initialize database indexes
try
{
    var databaseService = app.Services.GetRequiredService<DatabaseService>();
    await databaseService.InitializeIndexesAsync();
    
    if (await databaseService.TestConnectionAsync())
    {
        Console.WriteLine("✅ MongoDB connection successful!");
    }
    else
    {
        Console.WriteLine("❌ MongoDB connection failed!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database initialization error: {ex.Message}");
}

app.Run();

// Custom serializers for DateOnly and TimeOnly to work with MongoDB
public class DateOnlySerializer : SerializerBase<DateOnly>
{
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var dateTime = context.Reader.ReadDateTime();
        return DateOnly.FromDateTime(DateTime.FromBinary(dateTime));
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
    {
        var dateTime = value.ToDateTime(TimeOnly.MinValue);
        context.Writer.WriteDateTime(dateTime.ToBinary());
    }
}

public class TimeOnlySerializer : SerializerBase<TimeOnly>
{
    public override TimeOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var timeSpan = TimeSpan.FromTicks(context.Reader.ReadInt64());
        return TimeOnly.FromTimeSpan(timeSpan);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TimeOnly value)
    {
        context.Writer.WriteInt64(value.ToTimeSpan().Ticks);
    }
}
