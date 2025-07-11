using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using GurabaFiDunya.Models;

namespace GurabaFiDunya.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    
    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _key = _configuration["JWT:Key"] ?? throw new InvalidOperationException("JWT Key not found");
        _issuer = _configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found");
        _audience = _configuration["JWT:Audience"] ?? throw new InvalidOperationException("JWT Audience not found");
    }
    
    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_key);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_key);
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            
            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return principal;
        }
        catch
        {
            return null;
        }
    }
    
    public string? GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
} 