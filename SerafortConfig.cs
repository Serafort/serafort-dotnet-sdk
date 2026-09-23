using System.Text.Json.Serialization;

namespace Serafort.SDK
{
    public class SerafortConfig
    {
        public required string Endpoint { get; set; }
        public required string ClientId { get; set; }

        [JsonIgnore]
        public string? ClientSecret { get; set; }

        [JsonIgnore]
        public string? PrivateKey { get; set; }

        public override string ToString()
        {
            return $"SerafortConfig {{ Endpoint = {Endpoint}, ClientId = {ClientId}, ClientSecret = {(string.IsNullOrEmpty(ClientSecret) ? "(none)" : "***")}, PrivateKey = {(string.IsNullOrEmpty(PrivateKey) ? "(none)" : "***")} }}";
        }
    }
}
