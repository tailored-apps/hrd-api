using System;
using System.Threading.Tasks;
using Xunit;
using HRD.Api;
using System.IO;
using System.Text.Json;

namespace HRD.Api.Tests
{
    public class IntegrationTests
    {
        // Credentials provided by secrets.json
        private static string ApiKeyHex = null!;
        private static string Password = null!;
        private static string Login = null!;

        static IntegrationTests()
        {
            try
            {
                // Try to find secrets.json in repo root
                // We assume we are in bin/Debug/net9.0/
                // Repo root is likely 5 levels up? Or verify relative.
                // Or since we know the absolute path in this environment...
                // But better to be dynamic.
                var curr = Directory.GetCurrentDirectory();
                // Traverse up until we find secrets.json or hit root
                string path = "secrets.json";
                while (!File.Exists(path))
                {
                    var parent = Directory.GetParent(curr);
                    if (parent == null) break;
                    curr = parent.FullName;
                    path = Path.Combine(curr, "secrets.json");
                }

                if (!File.Exists(path))
                {
                    // Fallback for dev env absolute path usage if traversing fails
                    path = "/Users/kowalski/work/hrd-api/secrets.json";
                }

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    ApiKeyHex = root.GetProperty("apiKeyHex").GetString()!;
                    Password = root.GetProperty("password").GetString()!;
                    Login = root.GetProperty("login").GetString()!;
                }
            }
            catch
            {
                // Ignore or defaults
            }
        }

        [Fact()]
        public void PartnerGetBalance_IntegrationResult()
        {
            if (Login == "INSERT_LOGIN_HERE")
            {
                throw new Exception("Please provide the Login in IntegrationTests.cs");
            }

            using var api = HRDApi.Create(ApiKeyHex, Login, Password);

            var (balance, restricted) = api.PartnerGetBalance();

            Assert.True(balance >= 0);
            Assert.True(restricted >= 0);
        }

        [Fact]
        public void DomainCheck_IntegrationResult()
        {
            if (Login == "INSERT_LOGIN_HERE")
            {
                throw new Exception("Please provide the Login in IntegrationTests.cs");
            }

            using var api = HRDApi.Create(ApiKeyHex, Login, Password);

            var result = api.DomainCheck(new[] { "google.pl", "hrd.pl", "nonexistentdomain12345.pl" });

            Assert.NotNull(result);
            Assert.True(result.Count == 3);
            Assert.Contains("google.pl", result.Keys);
        }
    }
}
