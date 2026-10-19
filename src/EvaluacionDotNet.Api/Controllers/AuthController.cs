using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace EvaluacionDotNet.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(IConfiguration config) : ControllerBase
{
    [HttpPost("token")]
    public IActionResult Token()
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(2);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"], audience: config["Jwt:Audience"],
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "evaluacion"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: now, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer", expiresIn = 7200, expiresAtUtc = expires
        });
    }
}
