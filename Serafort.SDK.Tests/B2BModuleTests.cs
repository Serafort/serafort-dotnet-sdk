using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serafort.SDK;
using Serafort.SDK.B2B;
using Xunit;

namespace Serafort.SDK.Tests
{
    public class B2BModuleTests
    {
        private const string Issuer = "https://api.serafort.local";
        private const string Audience = "client1";

        private static (B2BModule Module, RsaSecurityKey Key) CreateModule()
        {
            var rsa = RSA.Create(2048);
            var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };

            var oidcConfig = new OpenIdConnectConfiguration { Issuer = Issuer };
            oidcConfig.SigningKeys.Add(key);

            var configurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidcConfig);
            var config = new SerafortConfig { Endpoint = Issuer, ClientId = Audience };
            var module = new B2BModule(config, configurationManager);
            return (module, key);
        }

        private static string CreateToken(RsaSecurityKey key, DateTime? expires = null, string issuer = Issuer, string audience = Audience)
        {
            var handler = new JwtSecurityTokenHandler();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("tenant_id", "tenant-1"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim("permissions", "org:write")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires ?? DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

            return handler.WriteToken(token);
        }

        [Fact]
        public async Task ValidateTokenAsync_ShouldReturnUserContext_ForValidToken()
        {
            var (module, key) = CreateModule();
            var token = CreateToken(key);

            var user = await module.ValidateTokenAsync(token);

            Assert.Equal("user-1", user.UserId);
            Assert.Equal("tenant-1", user.TenantId);
            Assert.Contains("admin", user.Roles);
            Assert.Contains("org:write", user.Permissions);
        }

        [Fact]
        public async Task ValidateTokenAsync_ShouldThrow_ForExpiredToken()
        {
            var (module, key) = CreateModule();
            var token = CreateToken(key, expires: DateTime.UtcNow.AddMinutes(-30));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => module.ValidateTokenAsync(token));
        }

        [Fact]
        public async Task ValidateTokenAsync_ShouldThrow_ForBadSignature()
        {
            var (module, _) = CreateModule();
            var otherRsa = RSA.Create(2048);
            var otherKey = new RsaSecurityKey(otherRsa) { KeyId = "other-key" };
            var token = CreateToken(otherKey);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => module.ValidateTokenAsync(token));
        }

        [Fact]
        public async Task ValidateTokenAsync_ShouldThrow_ForWrongIssuer()
        {
            var (module, key) = CreateModule();
            var token = CreateToken(key, issuer: "https://evil.example.com");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => module.ValidateTokenAsync(token));
        }

        [Fact]
        public void GetLoginUrl_ShouldRequireState()
        {
            var (module, _) = CreateModule();

            Assert.Throws<ArgumentException>(() => module.GetLoginUrl("tenant-1", "https://app.example.com/callback", state: ""));
        }

        [Fact]
        public void GetLoginUrl_ShouldIncludeStateAndEscapeParameters()
        {
            var (module, _) = CreateModule();

            var url = module.GetLoginUrl("tenant 1", "https://app.example.com/callback", state: "xyz123");

            Assert.Contains("state=xyz123", url);
            Assert.Contains("tenant=tenant%201", url);
        }
    }
}
