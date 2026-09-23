using System;

namespace Serafort.SDK.Internal
{
    internal static class EndpointGuard
    {
        public static void RequireHttps(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                throw new ArgumentException($"'{endpoint}' is not a valid absolute URL.", nameof(endpoint));

            bool isLocalDev = uri.IsLoopback;
            if (uri.Scheme != Uri.UriSchemeHttps && !isLocalDev)
                throw new ArgumentException(
                    $"SerafortConfig.Endpoint must use HTTPS (got '{uri.Scheme}'). Plaintext HTTP is only permitted against localhost/127.0.0.1 for local development.",
                    nameof(endpoint));
        }
    }
}
