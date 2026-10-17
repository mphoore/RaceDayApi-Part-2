using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDayApi.Models;
using Xunit;


namespace RaceDayApi.Tests
{
    public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;

        public ApiIntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // HELPER: Log in and get the JWT token
        private async Task<string> GetTokenAsync(string email, string password)
        {
            var loginRequest = new { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            return jsonResponse.GetProperty("token").GetString();
        }

        // HELPER: Set the Bearer token in the HTTP headers
        private void SetAuthHeader(string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ---------------------------------------------------------
        // 1. USER REGISTRATION AND LOGIN
        // ---------------------------------------------------------

        [Fact]
        public async Task Register_And_Login_ShouldSucceed()
        {
            // Arrange
            var randomEmail = $"testuser_{System.Guid.NewGuid()}@example.com";
            var registerRequest = new { Email = randomEmail, Password = "Password123!", Role = "Participant" };

            // Act - Register
            var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            // Assert - Register
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            // Act - Login
            var loginRequest = new { Email = randomEmail, Password = "Password123!" };
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert - Login
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var token = await GetTokenAsync(randomEmail, "Password123!");
            Assert.False(string.IsNullOrEmpty(token));
        }

        // ---------------------------------------------------------
        // 2. EVENT MANAGEMENT (ORGANISER ROLE)
        // ---------------------------------------------------------

        [Fact]
        public async Task Organiser_CanManageEvents()
        {
            // Arrange - Create an Organiser and get token
            var organiserEmail = $"org_{System.Guid.NewGuid()}@example.com";
            await _client.PostAsJsonAsync("/api/auth/register", new { Email = organiserEmail, Password = "Password123!", Role = "Organiser" });
            var token = await GetTokenAsync(organiserEmail, "Password123!");
            SetAuthHeader(token);

            var newEvent = new { EventName = "Test Marathon", EventType = "Run", Date = "2026-12-01", Location = "Test City" };

            // Act - Create Event
            var response = await _client.PostAsJsonAsync("/api/events", newEvent);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // ---------------------------------------------------------
        // 3. ROLE-BASED ACCESS ENFORCEMENT
        // ---------------------------------------------------------

        [Fact]
        public async Task Participant_CannotCreateEvent_ReturnsForbidden()
        {
            // Arrange - Create a Participant and get token
            var partEmail = $"part_{System.Guid.NewGuid()}@example.com";
            await _client.PostAsJsonAsync("/api/auth/register", new { Email = partEmail, Password = "Password123!", Role = "Participant" });
            var token = await GetTokenAsync(partEmail, "Password123!");
            SetAuthHeader(token);

            var newEvent = new { EventName = "Hacked Event", EventType = "Run", Date = "2026-12-01", Location = "Illegal" };

            // Act - Try to create event as Participant
            var response = await _client.PostAsJsonAsync("/api/events", newEvent);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Unauthenticated_Request_ReturnsUnauthorized()
        {
            // Act - Try to get events without a token
            _client.DefaultRequestHeaders.Authorization = null;
            var response = await _client.GetAsync("/api/events");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---------------------------------------------------------
        // 4. EVENT ENROLMENTS (PARTICIPANT ROLE)
        // ---------------------------------------------------------

        [Fact]
        public async Task Participant_CanEnrollInEvent()
        {
            // Note: This requires an Event and a Category to already exist in the database.
            // In a real scenario, you'd create an Organiser, create an Event, and a Category via the API first.
            // Assuming EventID = 1 and CategoryID = 1 exist in your test DB:

            // Arrange
            var partEmail = $"part_{System.Guid.NewGuid()}@example.com";
            await _client.PostAsJsonAsync("/api/auth/register", new { Email = partEmail, Password = "Password123!", Role = "Participant" });
            var token = await GetTokenAsync(partEmail, "Password123!");
            SetAuthHeader(token);

            var enrollRequest = new { CategoryID = 1 }; // Must be a valid CategoryID from your DB

            // Act
            var response = await _client.PostAsJsonAsync("/api/enrollments", enrollRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // Verify it was recorded
            var myEnrollmentsResponse = await _client.GetAsync("/api/enrollments/my-enrollments");
            Assert.Equal(HttpStatusCode.OK, myEnrollmentsResponse.StatusCode);

            var content = await myEnrollmentsResponse.Content.ReadAsStringAsync();
            Assert.Contains("Category", content); // Basic check to see if data returned
        }
    }


}
