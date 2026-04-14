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

public class UserRatingsControllerAddRatingTest
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
    public async Task addRating_ValidCredentials_ReturnsOk()
    {   //arrange
         // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");

        if(createdUser == null)
        {
            return;
        }
        
        ///////////////create a second account\\\\\\\\\\\\\
        UserRegistrationDto secondUser = new UserRegistrationDto()
        {
            Username = "Other User",
            FirstName = "Other",
            LastName = "User",
            Country = "US",
            Description = "tester",
            Email = "other@gmail.com",
            Password = "applesauce",
            ConfirmPassword = "applesauce",
            SecurityQuestion = "test question",
            SecurityAnswer = "applesauce"
        };

        await _userController.RegisterUserAccount(secondUser);

        // Fetch the second user from DB to get the real ID
        var otherUserAccount = await _context.UserAccounts
        .FirstOrDefaultAsync(u => u.UserSignupEmail == "other@gmail.com");

        UserRatingDto userRatingDto = new UserRatingDto
        {
            ReviewerId = createdUser.UserAccountId,
            RevieweeId = otherUserAccount!.UserAccountId,
            TimeManagementScore = 4,
            PaymentReliabilityScore = 4,
            CommunicationScore = 4,
            CollaborationScore = 4,
            RecommendationScore = 4
        };

        //act
        var result = await _userRatingsController.AddRating(userRatingDto);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null, "Expected OkObjectResult for valid rating");
        Assert.That(okResult!.StatusCode ?? StatusCodes.Status200OK, Is.EqualTo(StatusCodes.Status200OK));

        Assert.That(_context.UserRatings.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task addRating_InvalidToken_ReturnsUnauthorized()
    {
         // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");

        if(createdUser == null)
        {
            return;
        }
        
        //remove the token to simulate an invalid Token
        _userRatingsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() //no token
        };

         ///////////////create a second account\\\\\\\\\\\\\
        UserRegistrationDto secondUser = new UserRegistrationDto()
        {
            Username = "Other User",
            FirstName = "Other",
            LastName = "User",
            Country = "US",
            Description = "tester",
            Email = "other@gmail.com",
            Password = "applesauce",
            ConfirmPassword = "applesauce",
            SecurityQuestion = "test question",
            SecurityAnswer = "applesauce"
        };

        await _userController.RegisterUserAccount(secondUser);

        // Fetch the second user from DB to get the real ID
        var otherUserAccount = await _context.UserAccounts
        .FirstOrDefaultAsync(u => u.UserSignupEmail == "other@gmail.com");

        UserRatingDto userRatingDto = new UserRatingDto
        {
            ReviewerId = createdUser.UserAccountId,
            RevieweeId = otherUserAccount!.UserAccountId,
            TimeManagementScore = 4,
            PaymentReliabilityScore = 4,
            CommunicationScore = 4,
            CollaborationScore = 4,
            RecommendationScore = 4
        };

        //act
        var result = await _userRatingsController.AddRating(userRatingDto);

        // Assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult, Is.Not.Null, "should return 401 Unauthorized when the token is invalid or missing.");
        Assert.That(unauthorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));
        //to verify that there nothing was saved to the DB
        Assert.That(_context.UserRatings.Count(), Is.EqualTo(0));

    }

    [Test]
    public async Task addRating_ReviewerIdMismatch_returnsForbidden()
    {
        //arrange
         // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");

        if(createdUser == null)
        {
            return;
        }
        
        ///////////////create a second account\\\\\\\\\\\\\
        UserRegistrationDto secondUser = new UserRegistrationDto()
        {
            Username = "Other User",
            FirstName = "Other",
            LastName = "User",
            Country = "US",
            Description = "tester",
            Email = "other@gmail.com",
            Password = "applesauce",
            ConfirmPassword = "applesauce",
            SecurityQuestion = "test question",
            SecurityAnswer = "applesauce"
        };

        await _userController.RegisterUserAccount(secondUser);

        if(secondUser == null)
        {
            return;
        }

        // Fetch the second user from DB to get the real ID
        var otherUserAccount = await _context.UserAccounts
        .FirstOrDefaultAsync(u => u.UserSignupEmail == "other@gmail.com");

        UserRatingDto userRatingDto = new UserRatingDto
        {
            ReviewerId = otherUserAccount!.UserAccountId, //for the purpose of testing a mismatch to swap the user Id's (mismatch)
            RevieweeId = createdUser.UserAccountId,
            TimeManagementScore = 4,
            PaymentReliabilityScore = 4,
            CommunicationScore = 4,
            CollaborationScore = 4,
            RecommendationScore = 4
        };

        //act
        var result = await _userRatingsController.AddRating(userRatingDto);

        // Assert
        var forbidResult = result as ForbidResult;
        Assert.That(forbidResult, Is.Not.Null, "Expected a 403 Forbidden as you should not be able to rate with user Id being mismatched");
       
        //ensure nothing is saved to the DB
        Assert.That(_context.UserRatings.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task addRating_RatingOwnAccount_ReturnBadRequest()
    {
        //arrange
         // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");

        if(createdUser == null)
        {
            return;
        }
        


        UserRatingDto userRatingDto = new UserRatingDto
        {
            ReviewerId = createdUser.UserAccountId, 
            RevieweeId = createdUser.UserAccountId,// for the purpose of the test rating the user with the same ID (simulate rating oneself)
            TimeManagementScore = 4,
            PaymentReliabilityScore = 4,
            CommunicationScore = 4,
            CollaborationScore = 4,
            RecommendationScore = 4
        };

        //act
        var result = await _userRatingsController.AddRating(userRatingDto);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null, "Expected a 400 BadRequest as you should not be able to rate own account");
        Assert.That(badRequest!.StatusCode ?? StatusCodes.Status400BadRequest, Is.EqualTo(StatusCodes.Status400BadRequest));

        //ensure nothing is saved to the DB
        Assert.That(_context.UserRatings.Count(), Is.EqualTo(0));
    }


    [Test]
    public async Task addRating_RatingAccountNotExist_ReturnBadRequest()
    {
        // --- ARRANGE ---
        // 1. Get the Reviewer (Test User)
        var reviewer = await _context.UserAccounts.FirstAsync(u => u.UserSignupEmail == "test@gmail.com");

        // 2. Create a Reviewee manually, save them, then REMOVE them
        var ghostUser = new UserAccount 
        { 
            UserSignupEmail = "ghost@gmail.com", 
            HashedPassword = "Ghost",
            // ... add other required fields for your model ...
        };
        _context.UserAccounts.Add(ghostUser);
        await _context.SaveChangesAsync();
        
        int ghostId = ghostUser.UserAccountId;

        // Now, vanish them from the DB
        _context.UserAccounts.Remove(ghostUser);
        await _context.SaveChangesAsync();

        // 3. Prepare the DTO pointing to the deleted ID
        UserRatingDto userRatingDto = new UserRatingDto
        {
            ReviewerId = reviewer.UserAccountId,
            RevieweeId = ghostId, // This ID is now a hole in the DB
            TimeManagementScore = 5,
            PaymentReliabilityScore = 5,
            CommunicationScore = 5,
            CollaborationScore = 5,
            RecommendationScore = 5
        };

        // --- ACT ---
        var result = await _userRatingsController.AddRating(userRatingDto);

        // --- ASSERT ---
        // Check for BadRequestObjectResult specifically
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>(), "Should return BadRequest when reviewee is missing");
        
        var badRequest = (BadRequestObjectResult)result;
        
        // Check the message safely
        string? message = badRequest.Value?.ToString();
        Assert.That(message, Does.Contain("Unable to find accounts"), "Error message was not what we expected");

        // Double check DB is still empty of ratings
        Assert.That(await _context.UserRatings.CountAsync(), Is.EqualTo(0));
    }

}