using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dispatch.Infrastructure;

public sealed class EmissorDeTokenJwt(IOptions<JwtOptions> opcoes) : IEmissorDeToken
{
    public string EmitirToken(Usuario usuario)
    {
        var jwt = opcoes.Value;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ChaveDeAssinatura));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        // ClaimTypes.Role, não uma claim custom "papel": deixa o [Authorize(Roles = ...)] e
        // o RequireRole(...) do ASP.NET Core funcionarem prontos, sem policy customizada.
        //
        // "iat" explícito: essa sobrecarga de JwtSecurityToken não preenche IssuedAt sozinha (o
        // token saía sem a claim "iat" no payload) — só foi descoberto rodando de verdade
        // (dotnet build/test não notam ausência de claim). RF-01k (encerrar sessões antigas na
        // troca de senha, Program.cs OnTokenValidated) depende de IssuedAt ser real.
        var agora = DateTimeOffset.UtcNow;
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Role, usuario.Papel.ToString()),
            new(JwtRegisteredClaimNames.Iat, agora.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        ];

        var token = new JwtSecurityToken(
            issuer: jwt.Emissor,
            audience: jwt.Audiencia,
            claims: claims,
            expires: agora.AddMinutes(jwt.ExpiracaoMinutos).UtcDateTime,
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
