using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services;

using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using LoginRequest = EduVision.Models.DTO.Request.LoginRequest;
using RegisterRequest = EduVision.Models.DTO.Request.RegisterRequest;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly EduVisionContext _context;
    private readonly IEmailSender _emailSender;

    public AuthController(JwtService jwtService, EduVisionContext context, IEmailSender emailSender)
    {
        _jwtService = jwtService;
        _context = context;
        _emailSender = emailSender;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var user = _context.Users.FirstOrDefault(u => u.UserName == request.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(ApiResponse<object>.Fail("Invalid username or password", 401));


        var token = _jwtService.GenerateToken(user);

        var response = new LoginResponse
        {
            Token = token,
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return BadRequest(ApiResponse<string>.Fail("Email cannot be blank", 400));

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
        {
            if ((bool)existingUser.IsVerified)
                return BadRequest(ApiResponse<string>.Fail("Email has been registered", 400));

            // Tìm OTP ch?a s? d?ng và t?o cách ?ây d??i 1 phút
            var oldOtp = await _context.OtpTokens
                .Where(o => o.Email == request.Email && o.Used == false)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
            var otpCreatedAtUtc = DateTime.SpecifyKind((DateTime)oldOtp.CreatedAt, DateTimeKind.Utc);
            if (oldOtp != null && (DateTime.UtcNow - otpCreatedAtUtc).TotalMinutes < 1)
            {
                return Ok(ApiResponse<string>.Success("", "OTP has been sent. Please check your email."));
            }

            // T?o OTP m?i n?u ?ã h?t h?n ho?c không có
            var newOtp = new OtpToken
            {
                Email = request.Email,
                Token = GenerateOtpToken(),
                CreatedAt = DateTime.UtcNow,
                Used = false
            };
            _context.OtpTokens.Add(newOtp);
            await _context.SaveChangesAsync();

            await _emailSender.SendEmailAsync(request.Email, "EduVision registration OTP", $"Your OTP code is: {newOtp.Token}");

            return Ok(ApiResponse<string>.Success("", "New OTP has been sent again"));
        }

        // N?u user ch?a t?n t?i ? thêm m?i
        var newUser = new User
        {
            Email = request.Email,
            Role = (int)Role.USER,
            IsVerified = false
        };
        _context.Users.Add(newUser);

        var otp = new OtpToken
        {
            Email = request.Email,
            Token = GenerateOtpToken(),
            CreatedAt = DateTime.UtcNow,
            Used = false
        };
        _context.OtpTokens.Add(otp);

        await _context.SaveChangesAsync();
        await _emailSender.SendEmailAsync(request.Email, "EduVision registration OTP", $"Your OTP code is: {otp.Token}");

        return Ok(ApiResponse<string>.Success("", "OTP sent to email"));
    }



    [HttpPost("complete-registration")]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpToken)
            || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.FullName))
        {
            return BadRequest(ApiResponse<string>.Fail("Email, OTP, Full name, and Password must not be empty", 400));
        }

        var otp = _context.OtpTokens
            .Where(o => o.Email == request.Email && o.Token == request.OtpToken && o.Used == false)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (otp == null)
            return BadRequest(ApiResponse<string>.Fail("Invalid or already used OTP", 400));

        var otpCreatedAtUtc = DateTime.SpecifyKind((DateTime)otp.CreatedAt, DateTimeKind.Utc);

        if ((DateTime.UtcNow - otpCreatedAtUtc).TotalMinutes > 1)
            return BadRequest(ApiResponse<string>.Fail("OTP has expired", 400));

        var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);

        if (user == null)
            return BadRequest(ApiResponse<string>.Fail("User not found", 400));

        if (user.IsVerified == true)
            return BadRequest(ApiResponse<string>.Fail("Account already verified", 400));

        // ?ánh d?u OTP ?ã dùng
        otp.Used = true;

        // C?p nh?t thông tin user
        user.IsVerified = true;
        user.FullName = request.FullName;
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.UserName = user.Email;
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            Token = "",
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response, "Registration completed successfully"));
    }


    private string GenerateOtpToken()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString(); // mã OTP
    }
}





