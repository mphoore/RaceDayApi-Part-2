using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RaceDayApi.Models;


namespace RaceDayApi.Controllers
{
    [Route("api/events/{eventId}/categories")]
    [ApiController]
    [Authorize] // Must be logged in
    public class CategoriesController : ControllerBase
    {

        private readonly string _connectionString;

        public CategoriesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }


        // GET: api/events/{eventId}/categories
        [HttpGet]
        public async Task<IActionResult> GetCategories(int eventId)
        {
            List<Categories> categories = new List<Categories>();
            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = "SELECT * FROM CATEGORIES WHERE eventID = @EventID";
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@EventID", eventId);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Categories
                {
                    categoryID = (int)reader["categoryID"],
                    CategoryName = reader["CategoryName"].ToString(),
                    Distance = reader["Distance"].ToString(),
                    Fee = (decimal)reader["Fee"],
                    eventID = (int)reader["eventID"]
                });
            }
            return Ok(categories);
        }


        // POST: api/events/{eventId}/categories
        [HttpPost]
        [Authorize(Roles = "Organiser")] // Only Organisers can define categories
        public async Task<IActionResult> CreateCategory(int eventId, [FromBody] Categories newCategory)
        {
            var userId = User.FindFirst("UserID")?.Value;

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Security check: Ensure the Organiser creating the category actually owns the Event
            string checkOwnerQuery = @"SELECT COUNT(1) FROM EVENTS e 
                                   INNER JOIN ORGANISERS o ON e.organiserID = o.organiserID 
                                   WHERE e.eventID = @EventID AND o.userID = @UserID";
            using (SqlCommand checkCmd = new SqlCommand(checkOwnerQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@EventID", eventId);
                checkCmd.Parameters.AddWithValue("@UserID", userId);




                int count = (int)await checkCmd.ExecuteScalarAsync();
                if (count == 0) return Forbid("You can only add categories to your own events.");
            }

            string insertQuery = @"INSERT INTO CATEGORIES (CategoryName, Distance, Fee, eventID) 
                               VALUES (@Name, @Distance, @Fee, @EventID)";
            using SqlCommand cmd = new SqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("@Name", newCategory.CategoryName);
            cmd.Parameters.AddWithValue("@Distance", newCategory.Distance);
            cmd.Parameters.AddWithValue("@Fee", newCategory.Fee);
            cmd.Parameters.AddWithValue("@EventID", eventId);

            await cmd.ExecuteNonQueryAsync();
            return StatusCode(201, new { Message = "Category created successfully." });
        }

    }



}
