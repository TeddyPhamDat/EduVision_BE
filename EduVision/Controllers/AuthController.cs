using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services.Authentication;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2.Requests;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech.Transcription;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using static System.Net.WebRequestMethods;
using LoginRequest = EduVision.Models.DTO.Request.LoginRequest;
using RegisterRequest = EduVision.Models.DTO.Request.RegisterRequest;

/// <summary>
/// Controller for authentication and registration flows.
/// Follows RESTful conventions: pluralized, hyphenated nouns for resource URIs.
/// </summary>
[ApiController]
[Route("api/authentication")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly EduVisionContext _context;
    private readonly IEmailSender _emailSender;

    public AuthController(JwtService jwtService, EduVisionContext context, IEmailSender emailSender)
    {
        _jwtService = jwtService;
        _context = context;
        _emailSender = emailSender;
    }

    /// <summary>
    /// Authenticate user and return JWT token.
    /// <summary>
    /// Authenticate user and return JWT access + refresh token.
    /// </summary>
    [HttpPost("sessions")]
    public IActionResult CreateSession([FromBody] LoginRequest request)
    {
        var user = _context.Users.FirstOrDefault(u => u.UserName == request.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(ApiResponse<object>.Fail("Invalid username or password", 401));

        // Tạo access token + refresh token
        var tokens = _jwtService.GenerateTokens(user);

        // Kiểm tra refresh token đã tồn tại chưa
        var existingRefresh = _context.RefreshTokens.FirstOrDefault(rt => rt.UserId == user.UserId);

        if (existingRefresh != null)
        {
            // Cập nhật lại token và thời gian
            existingRefresh.Token = tokens.RefreshToken;
            existingRefresh.ExpiresAt = DateTime.UtcNow.AddDays(7);
            existingRefresh.IsRevoked = false;
            existingRefresh.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            // Chưa có thì thêm mới
            var newRefresh = new RefreshToken
            {
                UserId = user.UserId,
                Token = tokens.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };
            _context.RefreshTokens.Add(newRefresh);
        }

        _context.SaveChanges();

        // Trả về response
        var response = new LoginResponse
        {
            Token = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            TokenExpiresAt = tokens.AccessTokenExpireAt,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7), // cùng lúc với ExpiresAt
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response));
    }


    /// <summary>
    /// Refresh access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh-token")]
    public IActionResult RefreshToken([FromBody] EduVision.Models.DTO.Request.TokenRequest request)
    {
        var existingToken = _context.RefreshTokens
            .FirstOrDefault(rt => rt.Token == request.RefreshToken && !rt.IsRevoked);

        if (existingToken == null || existingToken.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid or expired refresh token", 401));
        }

        var user = _context.Users.FirstOrDefault(u => u.UserId == existingToken.UserId);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found", 401));
        }

        // Tạo access token mới + refresh token mới
        var tokens = _jwtService.GenerateTokens(user);

        // Cập nhật refresh token hiện tại
        existingToken.Token = tokens.RefreshToken;
        existingToken.ExpiresAt = DateTime.UtcNow.AddDays(7);
        existingToken.CreatedAt = DateTime.UtcNow;

        _context.SaveChanges();

        var response = new LoginResponse
        {
            Token = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            TokenExpiresAt = tokens.AccessTokenExpireAt,
            RefreshTokenExpiresAt = existingToken.ExpiresAt,
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response));
    }



    /// <summary>
    /// Initiate registration and send OTP to email.
    /// </summary>
    [HttpPost("registrations")]
    public async Task<IActionResult> CreateRegistration([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return BadRequest(ApiResponse<string>.Fail("Email cannot be blank", 400));

        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
        {
            if ((bool)existingUser.IsVerified)
                return BadRequest(ApiResponse<string>.Fail("Email has been registered", 400));

            var oldOtp = await _context.OtpTokens
                .Where(o => o.Email == request.Email && o.Used == false)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
            var otpCreatedAtUtc = DateTime.SpecifyKind((DateTime)oldOtp.CreatedAt, DateTimeKind.Utc);
            if (oldOtp != null && (DateTime.UtcNow - otpCreatedAtUtc).TotalMinutes < 1)
            {
                return Ok(ApiResponse<string>.Success("", "OTP has been sent. Please check your email."));
            }

            // Send a new OTP if the previous one is expired or used.
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

        // Create a new user and send OTP for verification.
        var newUser = new EduVision.Models.User
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

    /// <summary>
    /// Complete registration by verifying OTP and setting password.
    /// </summary>
    [HttpPost("registrations/complete")] 
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

        otp.Used = true;
        user.IsVerified = true;
        user.FullName = request.FullName;
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.UserName = user.Email;
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await _context.SaveChangesAsync();

        // Assign default quotas for new users.
        var periodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var quotas = new List<UserQuotum>
        {
            new UserQuotum { UserId = user.UserId, QuotaType = "video", QuotaLimit = 1, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new UserQuotum { UserId = user.UserId, QuotaType = "slides", QuotaLimit = 5, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _context.UserQuota.AddRange(quotas);
        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            Token = "",
            RefreshToken = "",
            TokenExpiresAt = null,
            RefreshTokenExpiresAt = null,
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response, "Registration completed successfully"));
    }

    /// <summary>
    /// Authenticate or register user using Google OAuth and return JWT token.
    /// </summary>
    [HttpPost("google-sessions")]
    public async Task<IActionResult> CreateGoogleSession([FromBody] GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
        }
        catch (InvalidJwtException)
        {
            return BadRequest(ApiResponse<string>.Fail("Invalid Token ID", 401));
        }

        var email = payload.Email;
        if (string.IsNullOrEmpty(email))
            return BadRequest(ApiResponse<string>.Fail("Can't get email from Google", 400));

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            if (user.IsVerified == false)
            {
                return BadRequest(ApiResponse<string>.Fail("The account has not been authenticated via OTP", 400));
            }
        }
        else
        {
            // Register new user from Google account.
            user = new EduVision.Models.User
            {
                Email = email,
                UserName = payload.Email,
                FullName = payload.Name,
                Role = (int)Role.USER,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Assign default quotas for new Google users.
            var periodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var quotas = new List<UserQuotum>
            {
                new UserQuotum { UserId = user.UserId, QuotaType = "video", QuotaLimit = 1, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new UserQuotum { UserId = user.UserId, QuotaType = "slides", QuotaLimit = 5, QuotaUsed = 0, PeriodStart = periodStart, PeriodEnd = periodEnd, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            _context.UserQuota.AddRange(quotas);
            await _context.SaveChangesAsync();
        }

        var tokens = _jwtService.GenerateTokens(user);

        var response = new LoginResponse
        {
            Token = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            TokenExpiresAt = null,
            RefreshTokenExpiresAt = null,
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = ((Role)user.Role).ToString()
        };

        return Ok(ApiResponse<LoginResponse>.Success(response, "Sign in successfully with Google"));
    }

    /// <summary>
    /// Initiates forgot password process: sends OTP to the user's email.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] EduVision.Models.DTO.Request.ForgotPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return BadRequest(ApiResponse<string>.Fail("Email is required", 400));

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        /// Fix for CS0019: Operator '||' cannot be applied to operands of type 'bool' and 'bool?'
        /// Updated the code to explicitly compare nullable bool with true.

        if (user == null || user.IsVerified != true)
            return BadRequest(ApiResponse<string>.Fail("Email is not registered or verified", 400));

        var otp = new OtpToken
        {
            Email = user.Email,
            Token = GenerateOtpToken(), // Random 6-digit code
            CreatedAt = DateTime.UtcNow,
            Used = false
        };

        _context.OtpTokens.Add(otp);
        await _context.SaveChangesAsync();

        // Gửi email chứa mã OTP ở đây (tùy tích hợp)
        await _emailSender.SendEmailAsync(user.Email, "EduVision Password Reset OTP", $"Your OTP code is: {otp.Token}");

        return Ok(ApiResponse<string>.Success("", "OTP has been sent to your email"));
    }

    /// <summary>
    /// Resets the password after verifying OTP.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] EduVision.Models.DTO.Request.ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpToken) || string.IsNullOrEmpty(request.NewPassword))
            return BadRequest(ApiResponse<string>.Fail("Email, OTP, and New Password are required", 400));

        var otp = await _context.OtpTokens
            .Where(o => o.Email == request.Email && o.Token == request.OtpToken && o.Used == false)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null || (DateTime.UtcNow - otp.CreatedAt.Value).TotalMinutes > 5)
            return BadRequest(ApiResponse<string>.Fail("Invalid or expired OTP", 400));

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return BadRequest(ApiResponse<string>.Fail("User not found", 400));

        // Cập nhật mật khẩu
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        otp.Used = true;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Success("Password has been reset successfully"));
    }


    /// <summary>
    /// Log out of the current instance by revoking the Refresh Token.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout([FromBody] EduVision.Models.DTO.Request.RefreshTokenRequest request)
    {
        var token = _context.RefreshTokens.FirstOrDefault(rt => rt.Token == request.RefreshToken);
        if (token == null)
        {
            return NotFound(ApiResponse<object>.Fail("Token not found", 404));
        }

        token.IsRevoked = true;
        _context.SaveChanges();

        return Ok(ApiResponse<object>.Success("Logged out successfully"));
    }

    /// <summary>
    /// Log out of all devices by revoking all user Refresh Tokens.
    /// </summary>
    [HttpPost("logout-all")]
    public IActionResult LogoutAll()
    {
        var userId = GetUserIdFromAccessToken(); // lấy từ ClaimsPrincipal

        var userTokens = _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToList();

        foreach (var token in userTokens)
        {

            token.IsRevoked = true;
        }

        _context.SaveChanges();

        return Ok(ApiResponse<object>.Success("All sessions logged out"));
    }

    private int GetUserIdFromAccessToken()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId");
        foreach (var claim in User.Claims)
        {
            Console.WriteLine($"{claim.Type}: {claim.Value}");
        }
        return int.Parse(userIdClaim?.Value ?? "0");
    }


    /// <summary>
    /// Generates a 6-digit OTP code for email verification.
    /// </summary>
    private string GenerateOtpToken()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}





