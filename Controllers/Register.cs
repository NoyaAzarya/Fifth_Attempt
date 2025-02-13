using Microsoft.AspNetCore.Mvc;
using Fifth_Attempt.Services; // For DatabaseService
using ClassLibrary5; // For User
using System;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;

namespace Fifth_Attempt.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DatabaseService _databaseService;

        public AuthController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User newUser)
        {
            // 1. Validate required fields
            if (string.IsNullOrWhiteSpace(newUser.FirstName) ||
                string.IsNullOrWhiteSpace(newUser.LastName) ||
                string.IsNullOrWhiteSpace(newUser.Email) ||
                string.IsNullOrWhiteSpace(newUser.Password)) // Ensure password is provided
            {
                return BadRequest(new
                {
                    Error = "All fields (FirstName, LastName, Email, Password) are required."
                });
            }

            // 2. Validate and normalize role
            newUser.Role = string.IsNullOrEmpty(newUser.Role) ? "student" : newUser.Role.ToLower();
            if (!new[] { "teacher", "student" }.Contains(newUser.Role))
            {
                return BadRequest(new
                {
                    Error = "Invalid role",
                    AllowedValues = new[] { "teacher", "student" }
                });
            }

            try
            {
                using var connection = _databaseService.GetConnection();
                await connection.OpenAsync();

                // 3. Check if email already exists (more efficient query)
                const string checkEmailQuery = "SELECT 1 FROM users WHERE email = @em LIMIT 1";
                using var cmdCheck = new MySqlCommand(checkEmailQuery, connection);
                cmdCheck.Parameters.AddWithValue("@em", newUser.Email);

                using (var reader = await cmdCheck.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return BadRequest(new { Error = "Email already taken." });
                    }
                }

                // 4. Hash the password before storing it
                string hashedPassword = HashPassword(newUser.Password);

                // 5. Insert new user (including password)
                const string insertQuery = @"
                    INSERT INTO users (first_name, last_name, email, role, password)
                    VALUES (@fn, @ln, @em, @rl, @pwd)";
                using var cmdInsert = new MySqlCommand(insertQuery, connection);
                cmdInsert.Parameters.AddWithValue("@fn", newUser.FirstName);
                cmdInsert.Parameters.AddWithValue("@ln", newUser.LastName);
                cmdInsert.Parameters.AddWithValue("@em", newUser.Email);
                cmdInsert.Parameters.AddWithValue("@rl", newUser.Role);
                cmdInsert.Parameters.AddWithValue("@pwd", hashedPassword);

                await cmdInsert.ExecuteNonQueryAsync();

                return Ok(new
                {
                    Message = "User registered successfully",
                    User = new
                    {
                        newUser.FirstName,
                        newUser.LastName,
                        newUser.Email,
                        newUser.Role
                    }
                });
            }
            catch (MySqlException sqlEx)
            {
                return StatusCode(500, new
                {
                    Error = "Database error",
                    Details = sqlEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Error = "Internal server error",
                    Details = ex.Message
                });
            }
        }

        // Password hashing function
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
