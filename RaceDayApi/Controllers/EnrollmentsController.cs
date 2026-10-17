using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RaceDayApi.Models;

namespace RaceDayApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    
    public class EnrollmentsController : ControllerBase
    {

        private readonly string _connectionString;

        public EnrollmentsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/enrollments/my-enrollments (Participant viewing their own)
        [HttpGet("my-enrollments")]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            var userId = User.FindFirst("UserID")?.Value;
            List<object> enrollments = new List<object>();

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Join tables to get ParticipantID based on JWT UserID, and fetch event/category details
            string query = @"
            SELECT e.EnrollmentID, e.RaceNumber, e.PaymentStatus, c.CategoryName, c.Distance, ev.EventName, ev.Date
            FROM ENROLLMENTS e
            INNER JOIN PARTICIPANTS p ON e.participantID = p.participantID
            INNER JOIN CATEGORIES c ON e.categoryID = c.categoryID
            INNER JOIN EVENTS ev ON c.eventID = ev.eventID
            WHERE p.userID = @UserID";

            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UserID", userId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                enrollments.Add(new
                {
                    EnrollmentID = reader["EnrollmentID"],
                    EventName = reader["EventName"].ToString(),
                    Category = reader["CategoryName"].ToString(),
                    Distance = reader["Distance"].ToString(),
                    RaceNumber = reader["RaceNumber"],
                    PaymentStatus = reader["PaymentStatus"].ToString()
                });

            }
            return Ok(enrollments);
        }



        // GET: api/events/{eventId}/enrollments (Organiser viewing all enrollments for an event)
        [HttpGet("/api/events/{eventId}/enrollments")]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> GetEventEnrollments(int eventId)
        {
            var userId = User.FindFirst("UserID")?.Value;
            List<object> enrollments = new List<object>();

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check if organiser owns event
            string checkQuery = @"SELECT COUNT(1) FROM EVENTS e 
                              INNER JOIN ORGANISERS o ON e.organiserID = o.organiserID 
                              WHERE e.eventID = @EventID AND o.userID = @UserID";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@EventID", eventId);
                checkCmd.Parameters.AddWithValue("@UserID", userId);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return Forbid("Access denied.");
            }

            string query = @"
            SELECT e.EnrollmentID, e.RaceNumber, e.PaymentStatus, p.FullName, c.CategoryName
            FROM ENROLLMENTS e
            INNER JOIN PARTICIPANTS p ON e.participantID = p.participantID
            INNER JOIN CATEGORIES c ON e.categoryID = c.categoryID
            WHERE c.eventID = @EventID";

            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@EventID", eventId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                enrollments.Add(new
                {
                    EnrollmentID = reader["EnrollmentID"],
                    ParticipantName = reader["FullName"].ToString(),
                    Category = reader["CategoryName"].ToString(),
                    RaceNumber = reader["RaceNumber"],
                    PaymentStatus = reader["PaymentStatus"].ToString()
                });
            }
            return Ok(enrollments);
        }


        // POST: api/enrollments
        [HttpPost]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> CreateEnrollment([FromBody] Enrollments request)
        {
            var userId = User.FindFirst("UserID")?.Value;

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Get ParticipantID
            string getPartQuery = "SELECT participantID FROM PARTICIPANTS WHERE userID = @UserID";
            int participantId = 0;
            using (SqlCommand getPartcmd = new SqlCommand(getPartQuery, conn))
            {
                getPartcmd.Parameters.AddWithValue("@UserID", userId);
                object result = await getPartcmd.ExecuteScalarAsync();
                if (result == null) return BadRequest("Participant profile not found.");
                participantId = (int)result;
            }

            // 2. Generate a random Race Number (e.g., between 1000 and 9999)
            Random rnd = new Random();
            int raceNumber = rnd.Next(1000, 9999);

            // 3. Insert Enrollment
            string insertQuery = @"INSERT INTO ENROLLMENTS (RaceNumber, PaymentStatus, participantID, categoryID) 
                               VALUES (@RaceNumber, 'Pending', @ParticipantID, @CategoryID)";
            using SqlCommand insertcmd = new SqlCommand(insertQuery, conn);
            insertcmd.Parameters.AddWithValue("@RaceNumber", raceNumber);
            insertcmd.Parameters.AddWithValue("@ParticipantID", participantId);
            insertcmd.Parameters.AddWithValue("@CategoryID", request.categoryID);

            await insertcmd.ExecuteNonQueryAsync();
            return StatusCode(201, new { Message = "Enrolled successfully.", RaceNumber = raceNumber });
        }
    }
}
