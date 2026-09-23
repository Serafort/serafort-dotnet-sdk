using System;
using System.Net.Http;
using Serafort.SDK.M2M;
using Serafort.SDK.B2B;

namespace Serafort.SDK
{
    public class SerafortClient : IDisposable
    {
        public SerafortConfig Config { get; }
        public M2MModule M2M { get; }
        public B2BModule B2B { get; }

        public SerafortClient(SerafortConfig config, HttpClient? httpClient = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            M2M = new M2MModule(config, httpClient);
            B2B = new B2BModule(config);
        }

        public void Dispose() => M2M.Dispose();
    }
}
