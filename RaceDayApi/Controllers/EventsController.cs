using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using RaceDayApi.Models;


namespace RaceDayApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EventsController : ControllerBase
    {

        private readonly string _connectionString;

        public EventsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            List<Events> events = new List<Events>();
            using SqlConnection conn = new SqlConnection(_connectionString);
            string query = "SELECT * FROM EVENTS";
            using SqlCommand cmd = new SqlCommand(query, conn);
            await conn.OpenAsync();


            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new Events
                {
                    eventID = (int)reader["eventID"],
                    EventName = reader["EventName"].ToString(),
                    EventType = reader["EventType"].ToString(),
                    Date = reader["Date"].ToString(),
                    Location = reader["Location"].ToString(),
                    organiserID = (int)reader["organiserID"]
                });
            }
            return Ok(events);
        }


        [HttpPost]
        [Authorize(Roles = "Organiser")] // Only Organisers can create
        public async Task<IActionResult> CreateEvent([FromBody] Events newEvent)
        {
            // Get OrganiserID from the JWT token claims
            var userIdClaim = User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Fetch the OrganiserID linked to this UserID
            string getOrgQuery = "SELECT organiserID FROM ORGANISERS WHERE userID = @UserID";
            int organiserId = 0;
            using (SqlCommand orgCmd = new SqlCommand(getOrgQuery, conn))
            {
                orgCmd.Parameters.AddWithValue("@UserID", userIdClaim);
                object result = await orgCmd.ExecuteScalarAsync();
                if (result == null) return BadRequest("Organiser profile not found.");
                organiserId = (int)result;
            }

            // Insert Event
            string insertQuery = @"INSERT INTO EVENTS (EventName, EventType, Date, Location, organiserID) 
                               VALUES (@Name, @Type, @Date, @Loc, @OrgID)";
            using SqlCommand cmd = new SqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@Name", newEvent.EventName);
            cmd.Parameters.AddWithValue("@Type", newEvent.EventType);
            cmd.Parameters.AddWithValue("@Date", newEvent.Date);
            cmd.Parameters.AddWithValue("@Loc", newEvent.Location);
            cmd.Parameters.AddWithValue("@OrgID", organiserId);

            await cmd.ExecuteNonQueryAsync();
            return StatusCode(201, new { Message = "Event created successfully." });
        }

        // You would add PUT and DELETE here similarly, marking them [Authorize(Roles = "Organiser")]
    }

}

