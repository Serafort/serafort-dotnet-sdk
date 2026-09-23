using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using RichardSzalay.MockHttp;
using Serafort.SDK;
using Serafort.SDK.M2M;
using Xunit;

namespace Serafort.SDK.Tests
{
    public class M2MModuleTests
    {
        [Fact]
        public async Task GetAccessTokenAsync_ShouldFetchAndCacheToken()
        {
            // Arrange
            var mockHttp = new MockHttpMessageHandler();
            
            mockHttp.Expect(HttpMethod.Post, "https://api.serafort.local/oauth/token")
                    .Respond("application/json", "{\"access_token\": \"test_token_123\", \"expires_in\": 3600}");

            var httpClient = mockHttp.ToHttpClient();
            var config = new SerafortConfig 
            { 
                Endpoint = "https://api.serafort.local", 
                ClientId = "client1", 
                ClientSecret = "secret1" 
            };
            var module = new M2MModule(config, httpClient);

            // Act 1: Fetch Token (Hits API)
            var token1 = await module.GetAccessTokenAsync();

            // Act 2: Fetch Token again (Hits Cache)
            var token2 = await module.GetAccessTokenAsync();

            // Assert
            Assert.Equal("test_token_123", token1);
            Assert.Equal("test_token_123", token2);
            mockHttp.VerifyNoOutstandingExpectation(); // Only 1 request was made
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldRetryOn500()
        {
            // Arrange
            var mockHttp = new MockHttpMessageHandler();
            
            // First request fails with 500
            mockHttp.Expect(HttpMethod.Post, "https://api.serafort.local/oauth/token")
                    .Respond(HttpStatusCode.InternalServerError);
                    
            // Second request succeeds
            mockHttp.Expect(HttpMethod.Post, "https://api.serafort.local/oauth/token")
                    .Respond("application/json", "{\"access_token\": \"retry_token_456\", \"expires_in\": 3600}");

            var httpClient = mockHttp.ToHttpClient();
            var config = new SerafortConfig 
            { 
                Endpoint = "https://api.serafort.local", 
                ClientId = "client2", 
                ClientSecret = "secret2" 
            };
            var module = new M2MModule(config, httpClient);

            // Act
            var token = await module.GetAccessTokenAsync();

            // Assert
            Assert.Equal("retry_token_456", token);
            mockHttp.VerifyNoOutstandingExpectation(); // Both requests were made
        }
    }
}
