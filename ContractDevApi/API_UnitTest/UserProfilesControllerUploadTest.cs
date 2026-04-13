using ContractDevApi.Controllers;
using ContractDevApi.DTOs;
using ContractDevApi.Models;
using ContractDevApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace API_UnitTest;

public class UserProfilesControllerUploadTest
{
    private UserAccountsController _userController = null!;
    private UserProfilesController _profileController = null!;
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
        //Assing dependency to build controller objects
        _userController = new UserAccountsController(_context, jwtService);
        _profileController = new UserProfilesController(_context, jwtService);
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
        //Register new user
        await _userController.RegisterUserAccount(newUser);
        UserLoginDto loginDto = new UserLoginDto
        {
            Email = "test@gmail.com",
            Password = "applesauce"
        };
        //Login new user after registration - capture return to verify login and assign verify token
        IActionResult loginResponse = await _userController.Login(loginDto);
        var okLoginResult = loginResponse as OkObjectResult;
        Assert.That(okLoginResult, Is.Not.Null, "Expected OkObjectResult for valid login");
        //Extract token from login response
        var tokenProperty = okLoginResult!.Value?.GetType().GetProperty("token");
        string token = tokenProperty?.GetValue(okLoginResult.Value)?.ToString() ?? string.Empty;
        Assert.That(token, Is.Not.Empty, "Expected token in login response");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "TestAuth"));
        //Add user's token to profile controller's httpcontext to simulate sending token as bearer via http request
        _profileController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task UploadValidFile()
    {
        //Arrange
        //Building in-memory png file 
        byte[] pngData = [1,2,3,4,5,6,7,8,9,0];
        await using var stream = new MemoryStream(pngData);
        IFormFile file = new FormFile(stream, 0, pngData.Length, "File", "pfp.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        //User will always be 1 as they are the only user in the in memory database
        ProfileUploadDto uploadDto = new ProfileUploadDto
        {
            Id = 1,
            File = file  
        };

        //Act
        IActionResult response = await _profileController.UploadFile(uploadDto);

        //Assert
        var okResult = response as OkObjectResult;
        Assert.That(okResult, Is.Not.Null, "Excepted OkObjectResult for valid PNG upload");
        Assert.That(okResult!.StatusCode ?? StatusCodes.Status200OK, Is.EqualTo(StatusCodes.Status200OK));

        //Validate that response returned new file path
        var pathProperty = okResult.Value?.GetType().GetProperty("path");
        string savedPath = pathProperty?.GetValue(okResult.Value)?.ToString() ?? string.Empty;
        Assert.That(savedPath, Is.Not.Empty, "Expected uploaded file path in response");
        //png file path should always start with /images/ and end with .png
        Assert.That(savedPath, Does.StartWith("/images/"));
        Assert.That(savedPath, Does.EndWith(".png"));
    }
    
    [Test]
    public async Task UploadInvalidFile()
    {
        //Arrange
        //Building in-memory text file 
        byte[] data = [1,2,3,4,5,6,7,8,9,0];
        await using var stream = new MemoryStream(data);
        IFormFile file = new FormFile(stream, 0, data.Length, "File", "data.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        //User will always be 1 as they are the only user in the in memory database
        ProfileUploadDto uploadDto = new ProfileUploadDto
        {
            Id = 1,
            File = file  
        };

        //Act
        IActionResult response = await _profileController.UploadFile(uploadDto);

        //Assert
        var badResult = response as BadRequestObjectResult;
        Assert.That(badResult, Is.Not.Null, "Excepted BadRequestObjectResult for invalid file upload");
        Assert.That(badResult!.StatusCode ?? StatusCodes.Status400BadRequest, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UploadEmptyFile()
    {
        //Arrange
        //Building in-memory text file 
        byte[] data = [];
        await using var stream = new MemoryStream(data);
        IFormFile file = new FormFile(stream, 0, data.Length, "File", "empty.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        //User will always be 1 as they are the only user in the in memory database
        ProfileUploadDto uploadDto = new ProfileUploadDto
        {
            Id = 1,
            File = file  
        };

        //Act
        IActionResult response = await _profileController.UploadFile(uploadDto);

        //Assert
        var badResult = response as BadRequestObjectResult;
        Assert.That(badResult, Is.Not.Null, "Excepted BadRequestObjectResult for invalid file upload");
        Assert.That(badResult!.StatusCode ?? StatusCodes.Status400BadRequest, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UploadInvalidAccount()
    {
        //Arrange
        //Building in-memory png file 
        byte[] pngData = [1,2,3,4,5,6,7,8,9,0];
        await using var stream = new MemoryStream(pngData);
        IFormFile file = new FormFile(stream, 0, pngData.Length, "File", "pfp.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        //User will always be 1 as they are the only user in the in memory database
        //Invalid test targets user that is not user 1 - NOTE: even if user 2 does not exist, UploadFile method will determine that token encoded user id does not match given id
        ProfileUploadDto uploadDto = new ProfileUploadDto
        {
            Id = 2,
            File = file  
        };

        //Act
        IActionResult response = await _profileController.UploadFile(uploadDto);

        //Assert
        var forbiddenResult = response as ForbidResult;
        Assert.That(forbiddenResult, Is.Not.Null, "Expected ForbidResult for mismatched User ID");
    }
}
