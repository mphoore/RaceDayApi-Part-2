using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RaceDayApi.Models;

namespace RaceDayApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ResultsController : ControllerBase
    {

        private readonly string _connectionString;

        public ResultsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/results/my-results
        [HttpGet("my-results")]
        [Authorize(Roles = "Participant")]
        public async Task<IActionResult> GetMyResults()
        {
            var userId = User.FindFirst("UserID")?.Value;
            List<object> results = new List<object>();

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Join Results -> Enrollments -> Participants -> Categories -> Events
            string query = @"
            SELECT r.ResultsID, r.FinishTime, r.Position, ev.EventName, c.CategoryName
            FROM RESULTS r
            INNER JOIN ENROLLMENTS e ON r.enrollmentID = e.enrollmentID
            INNER JOIN PARTICIPANTS p ON e.participantID = p.participantID
            INNER JOIN CATEGORIES c ON e.categoryID = c.categoryID
            INNER JOIN EVENTS ev ON c.eventID = ev.eventID
            WHERE p.userID = @UserID";

            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UserID", userId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    EventName = reader["EventName"].ToString(),
                    Category = reader["CategoryName"].ToString(),
                    FinishTime = reader["FinishTime"].ToString(),
                    Position = reader["Position"]
                });
            }
            return Ok(results);
        }

        // POST: api/results
        [HttpPost]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> CreateResult([FromBody] RaceDayApi.Models.Results request)
        {
            var userId = User.FindFirst("UserID")?.Value;

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Security Check: Ensure the Organiser owns the event that this Enrollment belongs to
            string checkOwnerQuery = @"
            SELECT COUNT(1) 
            FROM ENROLLMENTS e
            INNER JOIN CATEGORIES c ON e.categoryID = c.categoryID
            INNER JOIN EVENTS ev ON c.eventID = ev.eventID
            INNER JOIN ORGANISERS o ON ev.organiserID = o.organiserID
            WHERE e.enrollmentID = @EnrollmentID AND o.userID = @UserID";

            using (SqlCommand checkCmd = new SqlCommand(checkOwnerQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@EnrollmentID", request.enrollmentID);
                checkCmd.Parameters.AddWithValue("@UserID", userId);
                int count = (int)await checkCmd.ExecuteScalarAsync();
                if (count == 0) return Forbid("You do not have permission to log results for this enrollment.");
            }

            // Insert Result
            string insertQuery = @"INSERT INTO RESULTS (FinishTime, Position, enrollmentID) 
                               VALUES (@FinishTime, @Position, @EnrollmentID)";
            using SqlCommand cmd = new SqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@FinishTime", request.FinishTime);
            cmd.Parameters.AddWithValue("@Position", request.Position);
            cmd.Parameters.AddWithValue("@EnrollmentID", request.enrollmentID);

            await cmd.ExecuteNonQueryAsync();
            return StatusCode(201, new { Message = "Result captured successfully." });
        }

    }

}
