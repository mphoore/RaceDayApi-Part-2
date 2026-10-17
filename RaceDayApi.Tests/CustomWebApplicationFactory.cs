using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;


namespace RaceDayApi.Tests
{

    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Inject the test configuration, ensuring it points to your DB
                config.AddInMemoryCollection(new Dictionary<string, string>
            {
                {"ConnectionStrings:DefaultConnection", "Server=YOUR_SERVER_NAME;Database=RaceDay_Test;Trusted_Connection=True;TrustServerCertificate=True;"},
                {"Jwt:Key", "ThisIsAVerySecretKeyForRaceDayAPI2026!"},
                {"Jwt:Issuer", "RaceDayAPI"},
                {"Jwt:Audience", "RaceDayClient"}
            });
            });
        }
    }
}