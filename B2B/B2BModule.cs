using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serafort.SDK.Internal;

namespace Serafort.SDK.B2B
{
    public class UserContext
    {
        public required string UserId { get; set; }
        public required string TenantId { get; set; }
        public required List<string> Roles { get; set; }
        public required List<string> Permissions { get; set; }
    }

    public class B2BModule
    {
        private readonly SerafortConfig _config;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

        public B2BModule(SerafortConfig config)
            : this(config, CreateDefaultConfigurationManager(config))
        {
        }

        /// <summary>
        /// Accepts a custom <see cref="IConfigurationManager{OpenIdConnectConfiguration}"/> — primarily
        /// intended for tests, which can supply a <c>StaticConfigurationManager</c> instead of hitting a
        /// live discovery endpoint.
        /// </summary>
        public B2BModule(SerafortConfig config, IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
        {
            _config = config;
            _configurationManager = configurationManager;
        }

        private static ConfigurationManager<OpenIdConnectConfiguration> CreateDefaultConfigurationManager(SerafortConfig config)
        {
            EndpointGuard.RequireHttps(config.Endpoint);
            string discoveryEndpoint = $"{config.Endpoint.TrimEnd('/')}/.well-known/openid-configuration";
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                discoveryEndpoint,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }

        public async Task<UserContext> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var discoveryDocument = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var signingKeys = discoveryDocument.SigningKeys;

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _config.Endpoint,
                ValidateAudience = true,
                ValidAudience = _config.ClientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                // Pin accepted algorithms to prevent RS256 -> HS256 algorithm-confusion attacks.
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            };

            var handler = new JwtSecurityTokenHandler();
            try
            {
                var principal = handler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                var tenantId = principal.FindFirst("tenant_id")?.Value ?? "default";
                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                var roles = principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
                var permissions = principal.FindAll("permissions").Select(c => c.Value).ToList();

                return new UserContext
                {
                    UserId = userId,
                    TenantId = tenantId,
                    Roles = roles,
                    Permissions = permissions
                };
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Token validation failed.", ex);
            }
        }

        /// <summary>
        /// Builds the authorization redirect URL. <paramref name="state"/> is required and must be a
        /// cryptographically random value the caller generates and verifies on the callback (CSRF
        /// protection). Pass <paramref name="codeChallenge"/> once the authorization-code/PKCE exchange
        /// is implemented on the callback side.
        /// </summary>
        public string GetLoginUrl(string tenantId, string redirectUri, string state, string? codeChallenge = null, string codeChallengeMethod = "S256")
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("A cryptographically random 'state' value is required to protect against CSRF.", nameof(state));

            var url = $"{_config.Endpoint.TrimEnd('/')}/authorize" +
                      $"?tenant={Uri.EscapeDataString(tenantId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&state={Uri.EscapeDataString(state)}";

            if (!string.IsNullOrEmpty(codeChallenge))
                url += $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method={Uri.EscapeDataString(codeChallengeMethod)}";

            return url;
        }
    }
}
