using DataWebAPI.Models;
using DataWebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        public UsersController(IUserService service) => _service = service;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
        {
            var user = await _service.CreateUserAsync(req);
            return CreatedAtAction(nameof(Get), new { id = user.UserId }, user);   // 201 + Location header
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
            => Ok(await _service.GetUserAsync(id));

        [HttpGet("username/{username}")]
        public async Task<IActionResult> GetByUsername(string username)
            => Ok(await _service.GetUserByUsernameAsync(username));

        [HttpGet("email/{email}")]
        public async Task<IActionResult> GetByEmail(string email)
            => Ok(await _service.GetUserByEmailAsync(email));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
        {
            await _service.UpdateUserAsync(id, req);
            return NoContent();
        }

        // soft delete, the row stays because accounts point to it
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _service.DeactivateUserAsync(id);
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] UserQuery q)
            => Ok(await _service.SearchUsersAsync(q));

        [HttpPut("{id:int}/password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest req)
        {
            await _service.ResetPasswordAsync(id, req);
            return NoContent();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _service.LoginAsync(req);
            if (user == null)
                return Unauthorized(new
                {
                    error = "INVALID_CREDENTIALS",
                    message = "Username or password is incorrect.",
                    details = (object?)null
                });

            return Ok(user);
        }

        // form-data, key = file
        [HttpPost("{id:int}/picture")]
        public async Task<IActionResult> UploadPicture(int id, IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                return BadRequest(new
                {
                    error = "VALIDATION_ERROR",
                    message = "One or more fields are invalid.",
                    details = new { file = new[] { "Only .jpg, .jpeg and .png files are allowed." } }
                });

            if (file.Length == 0 || file.Length > 2 * 1024 * 1024)
                return BadRequest(new
                {
                    error = "VALIDATION_ERROR",
                    message = "One or more fields are invalid.",
                    details = new { file = new[] { "File must not be empty or bigger than 2 MB." } }
                });

            return Ok(await _service.SavePictureAsync(id, file));
        }

        [HttpGet("{id:int}/picture")]
        public async Task<IActionResult> GetPicture(int id)
        {
            var path = await _service.GetPictureFileAsync(id);
            var type = path.EndsWith(".png") ? "image/png" : "image/jpeg";
            return PhysicalFile(path, type);
        }
    }
}
