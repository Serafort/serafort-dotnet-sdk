# Serafort .NET SDK (`Serafort.SDK`)

Official .NET client library for the Serafort identity platform. Machine Identity (M2M) token caching with proactive refresh and Polly-backed retry, plus local B2B JWT validation against an OIDC discovery document, targeting .NET 8 LTS.

## Installation

```bash
dotnet add package Serafort.SDK
```

## Quickstart

```csharp
using Serafort.SDK;

var config = new SerafortConfig
{
    Endpoint = "https://auth.acme.com",
    ClientId = "my-client-id",
    ClientSecret = "my-client-secret"
};

using var client = new SerafortClient(config);

// 1. Retrieve M2M access token (cached, proactive 5-min refresh)
var token = await client.M2M.GetAccessTokenAsync(scopes: new[] { "read:users" });
Console.WriteLine($"Token: {token}");

// 2. Validate a user token locally (via OIDC discovery + JWKS)
var user = await client.B2B.ValidateTokenAsync(token);
Console.WriteLine($"User {user.UserId} authenticated in tenant {user.TenantId}");

// 3. RBAC checks
if (user.Permissions.Contains("org:write"))
{
    Console.WriteLine("Permission granted!");
}

// 4. Build a login redirect URL (state is required for CSRF protection)
var loginUrl = client.B2B.GetLoginUrl(tenantId: "acme", redirectUri: "https://app.acme.com/callback", state: Guid.NewGuid().ToString("N"));
```

## Security notes

- `SerafortConfig.Endpoint` must be `https://` (plain `http://` is only accepted against `localhost`/`127.0.0.1` for local development).
- `ClientSecret` and `PrivateKey` are excluded from JSON serialization and redacted in `ToString()`.
- JWT validation pins accepted algorithms to `RS256` to prevent algorithm-confusion attacks, and validates issuer, audience, signature, and lifetime.
- `GetLoginUrl` requires a caller-supplied `state` parameter and accepts an optional PKCE `codeChallenge` for when the authorization-code exchange is wired up.

## Roadmap (not yet implemented)

This v0.1 release covers the M2M client-credentials flow and B2B token validation. Not yet included, tracked as follow-up work:

- `IServiceCollection.AddSerafort(...)` dependency-injection extensions
- An ASP.NET Core authentication scheme (`AddAuthentication(...).AddSerafort(...)`)
- Platform secure token storage (`ITokenStore`, DPAPI-backed on Windows)
- Private-key JWT / mTLS client authentication (`SerafortConfig.PrivateKey` is reserved for this)

## Contributing

### Requirements

- .NET 8 SDK (tests additionally require the .NET 9 SDK)

### Git hooks

This repo ships a portable pre-commit hook under `.githooks/pre-commit` that runs `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet test` before every commit. It is **not** installed automatically — enable it once per clone with:

```bash
git config core.hooksPath .githooks
```

There is no Husky setup here: Husky is an npm-ecosystem tool that hooks into `package.json`/`node_modules`, and this is a pure .NET/NuGet project with no Node.js tooling involved. A plain POSIX shell script wired through `core.hooksPath` is the idiomatic equivalent for a .NET project and keeps it dependency-free.

### CI

Every push and pull request against `main` runs `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` via GitHub Actions (`.github/workflows/ci.yml`).
