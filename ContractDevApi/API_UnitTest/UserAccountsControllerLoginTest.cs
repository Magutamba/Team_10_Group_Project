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

public class UserProfilesControllerLoginTest
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
    public async Task LoginValidAccount()
    {
        //Arrange
        string validEmail = "test@gmail.com";
        string validPassword = "applesauce";
        
        UserLoginDto loginDto = new UserLoginDto()
        {
          Email = validEmail,
          Password = validPassword  
        };

        //Act
        IActionResult response = await _userController.Login(loginDto);

        //Assert
        var okResult = response as OkObjectResult;
        Assert.That(okResult, Is.Not.Null, "Expected OkObjectResult for valid login");
        Assert.That(okResult!.StatusCode ?? StatusCodes.Status200OK, Is.EqualTo(StatusCodes.Status200OK));

        //Verify that token is present in the response
        var tokenProperty = okResult!.Value?.GetType().GetProperty("token");
        string token = tokenProperty?.GetValue(okResult.Value)?.ToString() ?? string.Empty;
        Assert.That(token, Is.Not.Empty, "Expected JWT token from Login during test setup");
    }

    [Test]
    public async Task LoginInvalidEmail()
    {
        //Arrange
        string invalidEmail = "invalid@fake.qwerty";
        string validPassword = "applesauce";

        UserLoginDto loginDto = new UserLoginDto()
        {
          Email = invalidEmail,
          Password = validPassword  
        };

        //Act
        IActionResult response = await _userController.Login(loginDto);

        //Assert
        var unathorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unathorizedResult, Is.Not.Null, "Excepted Unauthorized for invalid login");
        Assert.That(unathorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task LoginInvalidPassword()
    {
        //Arrange
        string invalidEmail = "test@gmail.com";
        string validPassword = "wrongPw123";

        UserLoginDto loginDto = new UserLoginDto()
        {
          Email = invalidEmail,
          Password = validPassword  
        };

        //Act
        IActionResult response = await _userController.Login(loginDto);

        //Assert
        var unathorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unathorizedResult, Is.Not.Null, "Excepted Unauthorized for invalid login");
        Assert.That(unathorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task LoginEmptyData()
    {
        //Arrange
        UserLoginDto loginDto = new UserLoginDto(); //All fields empty

        //The api controller normally validates the following model state through the normal ASP.NET API pipeline
        //Requires that we define the model error here since we're testing and not using the full ASP.NET API pipeline
        _userController.ModelState.AddModelError("Email", "Email is required");
        _userController.ModelState.AddModelError("Password", "Password is required");

        //Act
        IActionResult response = await _userController.Login(loginDto);

        //Assert
        var badRequestResult = response as ObjectResult;
        Assert.That(badRequestResult, Is.Not.Null, "Expected object result for invalid model state");

        var problemDetails = badRequestResult.Value as ValidationProblemDetails;
        Assert.That(problemDetails, Is.Not.Null, "Expected ValidationProblemDetails payload");
        Assert.That(problemDetails!.Status ?? StatusCodes.Status400BadRequest, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(problemDetails!.Errors.ContainsKey("Email"), Is.True);
        Assert.That(problemDetails.Errors.ContainsKey("Password"), Is.True);
    }

    //--------
    //NOTE FOR REPORT:
    //Because of C# model binding and not utilizing the complete ASP pipeline, we cannot pass non UserLoginDto objects to the Login method,
    //We can neither assign non string values to email or password or null values for the same reason
    //Simply put the code will not compile due to errors raised
    //In-order to test sending non string values or non UserLoginDto object to the login endpoint we must use an HTTP Request via Integration Testing
    //Development EndPoint testing tool - Swagger - can be used to check this
    //--------
    // [Test]
    // public async Task LoginInvalidFields()
    // {
    //     //Arrange
    //     var json = JsonSerializer.Serialize(new {Email = 123, Password = false});

    //     //The api controller normally validates the following model state through the normal ASP.NET API pipeline
    //     //Requires that we define the model error here since we're testing and not using the full ASP.NET API pipeline
    //     _userController.ModelState.AddModelError("Email", "Email is required");
    //     _userController.ModelState.AddModelError("Password", "Password is required");

    //     //Act
    //     IActionResult response = await _userController.Login(json);
        
    // }
}
