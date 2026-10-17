using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using RaceDayApi.Models;

namespace RaceDayApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticatorController : ControllerBase
    {

        private readonly string _connectionString;
        private readonly IConfiguration _configuration;


        public AuthenticatorController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Users request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Role))
                return BadRequest("Email, Password, and Role are required.");

            if (request.Role != "Organiser" && request.Role != "Participant")
                return BadRequest("Role must be either 'Organiser' or 'Participant'.");

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();


            // Check if user exists
            string checkQuery = "SELECT COUNT(1) FROM USERS WHERE Email = @Email";
            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@Email", request.Email);
                int count = (int)await checkCmd.ExecuteScalarAsync();
                if (count > 0) return BadRequest("Email already exists.");
            }

            // Insert User
            string insertQuery = @"INSERT INTO USERS (Email, Password, Role) 
                               OUTPUT INSERTED.UserID 
                               VALUES (@Email, @Password, @Role)";
            int newUserId = 0;

            using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Email", request.Email);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);
                cmd.Parameters.AddWithValue("@Role", request.Role);
                newUserId = (int)await cmd.ExecuteScalarAsync();
            }

            // Insert into specific role table (Optional but recommended based on your schema)
            if (request.Role == "Participant")
            {
                string insertParticipant = "INSERT INTO PARTICIPANTS (FullName, Gender, DateOfBirth, UserID) VALUES ('', '', '', @UserID)";
                using SqlCommand cmd = new SqlCommand(insertParticipant, conn);
                cmd.Parameters.AddWithValue("@UserID", newUserId);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (request.Role == "Organiser")
            {
                string insertOrganiser = "INSERT INTO ORGANISERS (OrganiserName, UserID) VALUES ('', @UserID)";
                using SqlCommand cmd = new SqlCommand(insertOrganiser, conn);
                cmd.Parameters.AddWithValue("@UserID", newUserId);
                await cmd.ExecuteNonQueryAsync();
            }


            return Created("", new { Message = "User registered successfully." });
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Users request)
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = "SELECT UserID, Email, Password, Role FROM USERS WHERE Email = @Email";
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Email", request.Email);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string storedHash = reader["Password"].ToString();
                if (BCrypt.Net.BCrypt.Verify(request.Password, storedHash))
                {
                    var userRole = reader["Role"].ToString();
                    var userId = reader["UserID"].ToString();
                    var token = GenerateJwtToken(request.Email, userRole, userId);
                    return Ok(new { Token = token, Role = userRole });
                }

                 }
          return Unauthorized("Invalid credentials.");


        }

        private string GenerateJwtToken(string email, string role, string userId)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("UserID", userId)
            }),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }


    }
}
