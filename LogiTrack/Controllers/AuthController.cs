using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LogiTrack.Models;
using LogiTrack.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack.Controllers ;

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName
            };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Registration failed",
                    detail: string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, Roles.User);
            return Ok("User registered successfully");
        }

        [HttpPost]
        [Route("promote/{userName}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Promote(string userName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User not found",
                    detail: $"No user exists with username '{userName}'.");
            }

            if (!await _userManager.IsInRoleAsync(user, Roles.Admin))
            {
                await _userManager.AddToRoleAsync(user, Roles.Admin);
            }
            // The new role is in the user's next token, not any token they already hold
            return Ok($"{userName} is now an Admin");
        }

        [HttpPost]
        [Route("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.UserName);
            if (user == null)
            {
                return Unauthorized();
            }

            // Counts failures and locks the account after Lockout.MaxFailedAccessAttempts. A locked-out
            // login gets the same 401 as a wrong password or unknown user, so usernames can't be probed.
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Unauthorized();
            }

            var (token, expires) = await CreateTokenAsync(user);
            return Ok(new { token, expires });
        }

        private async Task<(string Token, DateTime Expires)> CreateTokenAsync(ApplicationUser user)
        {
            var jwt = _configuration.GetSection("Jwt");
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange((await _userManager.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var expires = DateTime.UtcNow.AddMinutes(jwt.GetValue("ExpiryMinutes", 60));
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
