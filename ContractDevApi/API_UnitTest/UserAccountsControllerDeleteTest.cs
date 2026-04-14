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

public class UserProfilesControllerDeleteTest
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
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    /*delete with valid credentials*/
    [Test]
    public async Task deleteAccount_ValidCredentials_ReturnsOk()
    {   
        
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        //fields
        int validId = createdUser!.UserAccountId;
        string validPassword = "applesauce";
        string validSecurityAnswer = "applesauce";

        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = validPassword,
            SecurityAnswer = validSecurityAnswer
        };

        //act
        IActionResult response = await _userController.DeleteUser(userDelete);

        //assert
        var okResult = response as OkObjectResult;
        Assert.That(okResult, Is.Not.Null, "Account OkObjectResult for valid account deletion");
        Assert.That(okResult!.StatusCode ?? StatusCodes.Status200OK, Is.EqualTo(StatusCodes.Status200OK));

        //verify the database is actually empty now
        var userInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(userInDb, Is.Null, "The user should have been removed from the database.");
    }
    
    /*delete with an InvalidPassword*/
    [Test]
    public async Task deleteAccount_InvalidPassword_ReturnsUnauthorized()
    {
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        //fields
        int validId = createdUser!.UserAccountId;
        string invalidPassword = "applesauc1";
        string validSecurityAnswer = "applesauce";

        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = invalidPassword,
            SecurityAnswer = validSecurityAnswer
        };

        //act
        IActionResult response = await _userController.DeleteUser(userDelete);

        //assert
        var unauthorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult, Is.Not.Null, "DeleteUser should return 401 Unauthorized when the security answer does not match the stored value.");
        Assert.That(unauthorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));

        //verify the database
        var userInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(userInDb, Is.Not.Null, "The user should not have been removed from the database.");
    }

    /*delete with a invalidSecurityAnswer*/
    [Test]
    public async Task deleteAccount_InvalidSecurityAnswer_ReturnsUnauthorized()
    {
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        //fields
        int validId = createdUser!.UserAccountId;
        string validPassword = "applesauce";
        string invalidSecurityAnswer = "applesauc1";

        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = validPassword,
            SecurityAnswer = invalidSecurityAnswer
        };

        //act
        IActionResult response = await _userController.DeleteUser(userDelete);

        //assert
        var unauthorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult, Is.Not.Null, "DeleteUser should return 401 Unauthorized when the security answer does not match the stored value.");
        Assert.That(unauthorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));

        //verify the database is actually empty now
        var userInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(userInDb, Is.Not.Null, "The user should not have been removed from the database.");
    }

    /*delete with a invalidToken*/
    [Test]
    public async Task deleteAccount_InvalidToken_ReturnsUnauthorized()
    {
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        //fields
        int validId = createdUser!.UserAccountId;

        //remove authentication from HttpContext
        _userController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() 
        };

        string validPassword = "applesauce";
        string validSecurityAnswer = "applesauce";

        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = validPassword,
            SecurityAnswer = validSecurityAnswer
        };

        //act
        IActionResult response = await _userController.DeleteUser(userDelete);

        //assert
        var unauthorizedResult = response as UnauthorizedObjectResult;
        Assert.That(unauthorizedResult, Is.Not.Null, "DeleteUser should return 401 Unauthorized when no token or an invalid token is provided.");
        Assert.That(unauthorizedResult!.StatusCode ?? StatusCodes.Status401Unauthorized, Is.EqualTo(StatusCodes.Status401Unauthorized));

        //verify the database
        var userInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(userInDb, Is.Not.Null, "The user should not have been removed from the database.");
    }

    /*delete with a Token user ID != dto.id (forbidden)*/
    [Test]
    public async Task deleteAccount_IdMismatch_ReturnsForbidden()
    {
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        //fields
        int validId = createdUser!.UserAccountId;
        string validPassword = "applesauce";
        string validSecurityAnswer = "applesauce";

        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = validPassword,
            SecurityAnswer = validSecurityAnswer
        };

        ///////////////create a second account\\\\\\\\\\\\\
        //create an instance of UserDeletionDto
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

        int otherUserId = otherUserAccount!.UserAccountId;

        //verify that other user exists in database
        Assert.That(otherUserAccount, Is.Not.Null, "Second user should exist in the database.");

        UserDeletionDto otherUser = new UserDeletionDto()
        {
            Id = otherUserId, // NOT the logged-in user
            Password = "applesauce",
            SecurityAnswer = "applesauce"
        };


        //act attempt to delete the account with the other users id
        IActionResult response = await _userController.DeleteUser(otherUser);

        //assert
        var forbidResult = response as ForbidResult;
        Assert.That(forbidResult, Is.Not.Null, "DeleteUser should return 403 Forbidden when a user attempts to delete another user's account.");
        
        // Verify the second user was NOT deleted
        var secondUserInDb = await _context.UserAccounts.FindAsync(otherUserId);
        Assert.That(secondUserInDb, Is.Not.Null, "The second user should not have been removed from the database.");

        // Verify the first user still exists
        var firstUserInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(firstUserInDb, Is.Not.Null, "The logged-in user should not have been removed from the database.");

    }

    [Test]
    public async Task deleteAccount_UserAccountNotFound_ReturnsNotFound()
    {
        // Fetch the user from the DB using the email to get the auto-generated ID
        var createdUser = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserSignupEmail == "test@gmail.com");
        
        if(createdUser == null)
        {
            return;
        }

        int validId = createdUser!.UserAccountId;
        string validPassword = "applesauce";
        string validSecurityAnswer = "applesauce";


        //remove only the user's profile to simulate missing profile "Profile Not Found"
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserAccountId == validId);

        //check if profile is not null
        if (profile != null)
        {
            //remove the profile
            _context.UserProfiles.Remove(profile);
            //update and save temp db
            await _context.SaveChangesAsync();
        }

       
        //create an instance of UserDeletionDto
        UserDeletionDto userDelete = new UserDeletionDto()
        {
            Id = validId,
            Password = validPassword,
            SecurityAnswer = validSecurityAnswer
        };

        //act
        IActionResult response = await _userController.DeleteUser(userDelete);

        //assert
        var notFoundResult = response as NotFoundObjectResult;
        Assert.That(notFoundResult, Is.Not.Null, "Should return NotFound when the profile is missing.");
        Assert.That(notFoundResult!.StatusCode ?? StatusCodes.Status404NotFound, Is.EqualTo(StatusCodes.Status404NotFound));

        //verify the UserAccount still exists (deletion shouldn't have gone through) 
        var userInDb = await _context.UserAccounts.FindAsync(validId);
        Assert.That(userInDb, Is.Not.Null, "The user should not have been removed from the database.");
    }
    

}