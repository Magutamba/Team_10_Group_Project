/*
 * @author Jeán Walton
 * 14/04/2026
 * UserRatingsControllerDeleteRatingTest.cs
 */

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
using Microsoft.AspNetCore.Http.HttpResults;

namespace API_UnitTest;

public class UserRatingsControllerDeleteRatingTest
{
    private UserAccountsController _userController = null!;
    private UserProfilesController _profileController = null!;
    private UserRatingsController _userRatingsController = null!;
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
        //Assigning dependency to build controller objects
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
        // Share the security context so _userController knows who the "Current User" is.
        _userController.ControllerContext = _profileController.ControllerContext;

        //Ratings controller
        _userRatingsController = new UserRatingsController(_context, jwtService);
        _userRatingsController.ControllerContext = new ControllerContext
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
    public async Task deleteRating_InvalidUserRatingDeleteDto_ReturnsValidationProblem()
    {   //arrange
        _userRatingsController.ModelState.AddModelError("ReviewerId", "Required"); //"Required should equal RevieweeId

        UserRatingDeleteDto userRatingDeleteDto = new UserRatingDeleteDto();
        //act
        var result = await _userRatingsController.DeleteRating(userRatingDeleteDto);
        // assert
         var obj = result as ObjectResult;//objectResult does not return a status code with ValidationProblem()
        Assert.That(obj, Is.Not.Null, "Expected ValidationProblem to return an ObjectResult");
        // Accept both valid behaviors
        Assert.That(obj.StatusCode == null || obj.StatusCode == 400);
    }

    [Test]
    public async Task deleteRating_InvalidToken_ReturnsUnauthorized()
    {   //arrange
        // You must wipe the context of the controller we actually testing
        _userRatingsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() // No User, No Claims, No Token
        };

        UserRatingDeleteDto userRatingDeleteDto = new UserRatingDeleteDto
        {
            RevieweeId = 1,
            ReviewerId = 2
        };

        //act
        var result = await _userRatingsController.DeleteRating(userRatingDeleteDto);

        //assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult, Is.Not.Null, "Expected UnauthorizedObjectResult");
        Assert.That(unauthorizedResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task deleteRating_ReviewerIdMismatch_ReturnsForbidden()
    {   //arrange
        var user = await _context.UserAccounts.FirstAsync(u => u.UserSignupEmail == "test@gmail.com");

        var dto = new UserRatingDeleteDto
        {
            ReviewerId = user.UserAccountId + 222, // mismatch the id for testing purposes
            RevieweeId = user.UserAccountId
        };
        //act
        var result = await _userRatingsController.DeleteRating(dto);
        //assert
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task deleteRating_RatingNotFound_ReturnsBadRequest()
    {   //arrange
        var user = await _context.UserAccounts.FirstAsync(u => u.UserSignupEmail == "test@gmail.com");

        var dto = new UserRatingDeleteDto
        {
            ReviewerId = user.UserAccountId,
            RevieweeId = 123 // creating a nonexistent id for test purpose 
        };
        //act
        var result = await _userRatingsController.DeleteRating(dto);
        //assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task deleteRating_ValidCredentials_ReturnsOk()
    {   //arrange
       var reviewer = await _context.UserAccounts.FirstAsync(u => u.UserSignupEmail == "test@gmail.com");

        UserAccount reviewee = new UserAccount
        {
            UserSignupEmail = "other@gmail.com",
            HashedPassword = "password",
            SecurityAnswer = "password"
        };

        _context.UserAccounts.Add(reviewee);
        await _context.SaveChangesAsync();

        var rating = new UserRating
        {
            ReviewerId = reviewer.UserAccountId,
            RevieweeId = reviewee.UserAccountId
        };

        _context.UserRatings.Add(rating);
        await _context.SaveChangesAsync();

        var dto = new UserRatingDeleteDto
        {
            ReviewerId = reviewer.UserAccountId,
            RevieweeId = reviewee.UserAccountId
        };
        //act
        var result = await _userRatingsController.DeleteRating(dto);
        //assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        //check db
        Assert.That(_context.UserRatings.Count(), Is.EqualTo(0)); 
    }
}