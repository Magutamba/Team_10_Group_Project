using ContractDevApi.Controllers;
using ContractDevApi.DTOs;
using ContractDevApi.Models;
using ContractDevApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace API_UnitTest;

public class UserProfilesControllerValidateTokenTest
{
    private UserAccountsController _userController = null!;
    private ContractDevContext _context = null!;

    [SetUp]
    public async Task Setup()
    {
        //Build inmemory database
        var options = new DbContextOptionsBuilder<ContractDevContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase" + Guid.NewGuid())
            .Options;
        //apply inmemory database to context for model sync connection
        _context = new ContractDevContext(options);
        //JWT setup
        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "supersecretkey1234567890!@#$%^&*()",
            ["Jwt:Issuer"] = "https://localhost:7186",
            ["Jwt:Audience"] = "http://localhost:4200",
            ["Jwt:ExpiresInMinutes"] = "60"
        };
        //Build testing app
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        //Build JWT object
        var jwtService = new JwtService(configuration);
        //Assign dependency to build controller object
        _userController = new UserAccountsController(_context, jwtService);
        
        UserRegistrationDto newUser = new UserRegistrationDto(){
            Username = "Test User",
            FirstName = "Test",
            LastName = "User",
            Country = "US",
            Description = "developer",
            Email = "test@gmail.com",
            Password = "applesauce",
            ConfirmPassword = "applesauce",
            SecurityQuestion = "test question",
            SecurityAnswer = "applesauce"
        };
        //register test user
        await _userController.RegisterUserAccount(newUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task ValidateTokenAuthenticatedUser()
    {
        //Arrange
        UserLoginDto loginDto = new UserLoginDto
        {
            Email = "test@gmail.com",
            Password = "applesauce"
        };

        IActionResult response = await _userController.Login(loginDto);
        var okLoginResult = response as OkObjectResult;
        Assert.That(okLoginResult, Is.Not.Null, "Expected OkObjectResult for valid login");

        var tokenProperty = okLoginResult!.Value?.GetType().GetProperty("token");
        string token = tokenProperty?.GetValue(okLoginResult.Value)?.ToString() ?? string.Empty;
        Assert.That(token, Is.Not.Empty, "Expected token in login response");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "TestAuth"));

        _userController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        //Act
        response = await _userController.ValidateToken();

        //Assert
        var okValidateResult = response as OkObjectResult;
        Assert.That(okValidateResult, Is.Not.Null, "Expected OkObjectResult for valid token");
        Assert.That(okValidateResult!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task ValidateTokenUnauthenticatedUser()
    {
        //Arrange
        UserRegistrationDto newUser = new UserRegistrationDto(){
            Username = "Test User2",
            FirstName = "Test",
            LastName = "User2",
            Country = "US",
            Description = "developer",
            Email = "test@outlook.com",
            Password = "applesauce",
            ConfirmPassword = "applesauce",
            SecurityQuestion = "test question",
            SecurityAnswer = "applesauce"
        };
        //Register new user
        await _userController.RegisterUserAccount(newUser);

        //Login new user
        UserLoginDto loginDto = new UserLoginDto
        {
            Email = "test@outlook.com",
            Password = "applesauce"
        };
        IActionResult response = await _userController.Login(loginDto);
        var okLoginResult = response as OkObjectResult;
        Assert.That(okLoginResult, Is.Not.Null, "Expected OkObjectResult for valid login");

        //No capturing token from Login response - attempt to validate user without adding them as principal user in http context using token

        //Act
        response = await _userController.ValidateToken();

        //Assert
        var unathorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unathorizedResult, Is.Not.Null, "Expected UnauthorizedObjectResult for invalid token");
        Assert.That(unathorizedResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }
    
}
