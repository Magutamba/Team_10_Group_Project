using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using ContractDevApi.DTOs;
using ContractDevApi.Models;
using ContractDevApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ContractDevApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfilesController : ControllerBase
    {
        private readonly ContractDevContext _context;

        private readonly JwtService _jwt;

        //Controller Constructor, builds inmemory database context and JWT token service
        public UserProfilesController(ContractDevContext context, JwtService jwt)
        {
            _context = context;
            _jwt = jwt;
        }
        //*Moïse | CloudFront safe api rout for profile image retrieval
        private static string BuildProfileImageApiPath(string? storedPath)
        {
            if(string.IsNullOrWhiteSpace(storedPath))
            {
                return string.Empty;
            }

            var fileName = Path.GetFileName(storedPath);
            if(string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return $"/api/UserProfiles/Image/{fileName}";
        }

        //-----------------------
        //Update Profle - Requires that logged in user is authenticated, Checks JWT and Cookie auth
        //Recieves id of user from Front-End and updated profile fields
        //Empty or null values are ignored for updated fields - prevents dataloss
        //-----------------------
        [Authorize]
        [HttpPut("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto dto)
        {
            //Get the authenticated user ID from JWT token claim
            var authenticatedUserId = GetAuthenticatedUserId();
            if (authenticatedUserId == null)
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            //Verify the authenticated user is trying to change their own profile
            if (authenticatedUserId.Value != dto.Id)
            {
                return Forbid(); // 403 Forbidden - user is authenticated but attempting to change the profile of another user
            }

            var user = await _context.UserAccounts.FindAsync(dto.Id);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(x => x.UserAccountId == dto.Id);
            var social = await _context.SocialConnections.FirstOrDefaultAsync(x => x.UserAccountId == dto.Id);

            if (user == null || profile == null || social == null) return NotFound("User not found");
            //hasChanges flag to check if any changes have been made to the profile - prevent database update if no changes detected
            bool hasChanges = false;
            //Check each value to determine if not null and does not match existing value on database
            if (dto.Username != null && profile.Username != dto.Username.ToLower()) { profile.Username = dto.Username.ToLower(); hasChanges = true; }
            if (dto.Email != null && user.UserSignupEmail != dto.Email.ToLower()) { user.UserSignupEmail = dto.Email.ToLower(); hasChanges = true; }
            if (dto.PhoneNumber != null && profile.PhoneNumber != dto.PhoneNumber) { profile.PhoneNumber = dto.PhoneNumber; hasChanges = true; }
            if (dto.Country != null && profile.Country != dto.Country) { profile.Country = dto.Country; hasChanges = true; }
            if (dto.UserTitle != null && profile.UserTitle != dto.UserTitle) { profile.UserTitle = dto.UserTitle; hasChanges = true; }
            if (dto.Bio != null && profile.Bio != dto.Bio) { profile.Bio = dto.Bio; hasChanges = true; }
            if (dto.AvailableForWork.HasValue && profile.AvailableForWork != dto.AvailableForWork) { profile.AvailableForWork = dto.AvailableForWork; hasChanges = true; }
            if (dto.OfferingWork.HasValue && profile.OfferingWork != dto.OfferingWork) { profile.OfferingWork = dto.OfferingWork; hasChanges = true; }
            if (dto.DisplayUserName.HasValue && profile.UsernameDisplay != dto.DisplayUserName) { profile.UsernameDisplay = dto.DisplayUserName; hasChanges = true; }
            if (dto.HidePhoneNumber.HasValue && profile.HidePhoneNumber != dto.HidePhoneNumber) { profile.HidePhoneNumber = dto.HidePhoneNumber; hasChanges = true; }
            if (dto.FacebookLink != null && social.FacebookLink != dto.FacebookLink) { social.FacebookLink = dto.FacebookLink; hasChanges = true; }
            if (dto.UserSocialEmailLink != null && social.UserSocialEmailLink != dto.UserSocialEmailLink) { social.UserSocialEmailLink = dto.UserSocialEmailLink; hasChanges = true; }
            if (dto.XLink != null && social.XLink != dto.XLink) { social.XLink = dto.XLink; hasChanges = true; }
            if (dto.GithubLink != null && social.GithubLink != dto.GithubLink) { social.GithubLink = dto.GithubLink; hasChanges = true; }
            if (dto.LinkedinLink != null && social.LinkedinLink != dto.LinkedinLink) { social.LinkedinLink = dto.LinkedinLink; hasChanges = true; }

            //Get all skill IDs from database that match skills provided in DTO
            var dbSkillIds = await _context.Skills
                .Where(x => dto.Skills.Contains(x.SkillName))
                .Select(x => x.SkillId).ToListAsync();
            //Get all user skills current to db
            var savedSkillIds = await _context.UserSkills
                .Where(x => x.UserAccountId == dto.Id)
                .Select(x => x.SkillId).ToListAsync();
            //Check if skills list differs - by number of skills or by skill ids
            bool skillsChanged = dbSkillIds.Count != savedSkillIds.Count || dbSkillIds.Except(savedSkillIds).Any();

            if (skillsChanged)
            {
                hasChanges = true;
                //Remove existing skills
                var oldSkills = _context.UserSkills.Where(x => x.UserAccountId == dto.Id);
                _context.UserSkills.RemoveRange(oldSkills);
                //Add new skills
                var newSkills = dbSkillIds.Select(skillId => new UserSkill
                {
                    UserAccountId = dto.Id,
                    SkillId = skillId
                });
                _context.UserSkills.AddRange(newSkills);
            }

            //Final check for changes
            if (!hasChanges)
            {
                return Ok(new { updated = false, Message = "No changes detected in profile update" });
            }

            //Try to update database -- if unsuccessful return error
            try {
                await _context.SaveChangesAsync();
            } catch(DbUpdateException e) {
                return Problem("System error occured. User Profile Update Failed."+e.Message);
            }


            return Ok(new { updated = true, message = "Profile updated successfully" });
        }

        //-----------------------
        //Get Profile Details - receives user id from Front-End
        //Construct full user and profile entity from shared userid and sends that back to front-end as response
        //-----------------------
        [Authorize]
        [HttpGet("ProfileDetails")]
        public async Task<IActionResult> GetProfileDetails([FromQuery] int id)
        {
            //Get the authenticated user ID from JWT token claim
            var authenticatedUserId = GetAuthenticatedUserId();
            if (authenticatedUserId == null)
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            //Verify the authenticated user is trying to get their own details
            if (authenticatedUserId.Value != id)
            {
                return Forbid(); // 403 Forbidden - user is authenticated but attempting to retrieve the details of another user
            }

            var user = await _context.UserAccounts.FindAsync(id);
            if (user == null) return NotFound("User not found");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(x => x.UserAccountId == id);
            if (profile == null) return NotFound("Profile not found");

            var social = await _context.SocialConnections.FirstOrDefaultAsync(x => x.UserAccountId == id);
            if (social == null) social = new SocialConnection();

            var ratings = await _context.UserRatings.Where(x => x.RevieweeId == id).ToListAsync();
            var hasRatings = ratings.Count > 0;
            var timeManagement = hasRatings ? Math.Round((decimal)ratings.Average(x => x.TimeManagementScore), 1) : 0m;
            var paymentReliability = hasRatings ? Math.Round((decimal)ratings.Average(x => x.PaymentReliabilityScore), 1) : 0m;
            var communication = hasRatings ? Math.Round((decimal)ratings.Average(x => x.CommunicationScore), 1) : 0m;
            var collaboration = hasRatings ? Math.Round((decimal)ratings.Average(x => x.CollaborationScore), 1) : 0m;
            var recommendation = hasRatings ? Math.Round((decimal)ratings.Average(x => x.RecommendationScore), 1) : 0m;
            var totalReviewScore = hasRatings
                ? Math.Round((decimal)ratings.Average(x =>
                    (x.TimeManagementScore + x.PaymentReliabilityScore + x.CommunicationScore + x.CollaborationScore + x.RecommendationScore) / 5.0), 1)
                : 0m;
            
            var skills = await _context.UserSkills.Where(x => x.UserAccountId == id).Select(x => x.Skill!.SkillName).ToListAsync();

            var response = new ProfileResponseDto
            {
                UserId = user.UserAccountId,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Username = profile.Username,
                Email = user.UserSignupEmail,
                PhoneNumber = profile.PhoneNumber,
                Country = profile.Country,
                UserTitle = profile.UserTitle,
                Bio = profile.Bio,
                SecurityQuestion = user.SecurityQuestion,
                AvailableForWork = profile.AvailableForWork,
                OfferingWork = profile.OfferingWork,
                DisplayUserName = profile.UsernameDisplay,
                HidePhoneNumber = profile.HidePhoneNumber,
                //ProfileImagePath = profile.ProfilePictureFilepath,
                //*Moïse | return api route instead of direct /image so CloudFront routes to backend on EC2 properly
                ProfileImagePath = BuildProfileImageApiPath(profile.ProfilePictureFilepath),
                Socials = new Dictionary<string, string?> {
                        { "facebook", social.FacebookLink },
                        { "Social Email", social.UserSocialEmailLink },
                        { "X", social.XLink },
                        { "Github", social.GithubLink },
                        { "LinkedIn", social.LinkedinLink }
                },
                Ratings = new Dictionary<string, decimal> {
                    { "Time Management", timeManagement },
                    { "Payment Reliability", paymentReliability },
                    { "Communication", communication },
                    { "Collaboration", collaboration },
                    { "Recommendation", recommendation },
                    { "Total Review Score", totalReviewScore }
                },
                Skills = skills
            };

            return Ok(response);
        }

        //-----------------------
        //Get All Users - Used in Front-End to build gallary of user profiles
        //Method uses linq query to construct complete list of user entities
        //Joins both tables on shared attribute - UserAccountId
        //Reponds to Front-End with List of Profile Response Dto, with expected naming scheme on Front-End
        //-----------------------
        [Authorize]
        [HttpGet("ProfileGallery")]
        public async Task<IActionResult> GetAllUsers()
        {
            var profiles = await _context.UserProfiles.ToListAsync();
            var users = await _context.UserAccounts.ToListAsync();
            var socials = await _context.SocialConnections.ToListAsync();
            var ratings = await _context.UserRatings.ToListAsync();
            
            var skills =
                from us in _context.UserSkills
                join s in _context.Skills on us.SkillId equals s.SkillId
                group s.SkillName by us.UserAccountId into g
                select new
                {
                    UserAccountId = g.Key,
                    Skills = g.ToList()
                };

            //Get average of all scores per user
            var aggregatedRatings =
                from rating in ratings
                group rating by rating.RevieweeId into g
                select new
                {
                    UserAccountId = g.Key,
                    TimeManagement = Math.Round((decimal)g.Average(x => x.TimeManagementScore), 1),
                    PaymentReliability = Math.Round((decimal)g.Average(x => x.PaymentReliabilityScore), 1),
                    Communication = Math.Round((decimal)g.Average(x => x.CommunicationScore), 1),
                    Collaboration = Math.Round((decimal)g.Average(x => x.CollaborationScore), 1),
                    Recommendation = Math.Round((decimal)g.Average(x => x.RecommendationScore), 1),
                    TotalReviewScore = Math.Round((decimal)g.Average(x =>
                        (x.TimeManagementScore + x.PaymentReliabilityScore + x.CommunicationScore + x.CollaborationScore + x.RecommendationScore) / 5.0), 1)
                };

            var response =
                from u in users
                    join p in profiles 
                    on u.UserAccountId equals p.UserAccountId 
                    into profileJoin
                from p in profileJoin.DefaultIfEmpty()
                    join s in socials 
                    on u.UserAccountId equals s.UserAccountId 
                    into socialJoin
                from s in socialJoin.DefaultIfEmpty()
                    join ar in aggregatedRatings 
                    on u.UserAccountId equals ar.UserAccountId 
                    into ratingJoin
                from ar in ratingJoin.DefaultIfEmpty()
                    join sk in skills on u.UserAccountId 
                    equals sk.UserAccountId 
                    into skillJoin
                from sk in skillJoin.DefaultIfEmpty()
                select new ProfileResponseDto
                {
                    UserId = u.UserAccountId,
                    FirstName = p?.FirstName ?? string.Empty,
                    LastName = p?.LastName ?? string.Empty,
                    Username = p?.Username ?? string.Empty,
                    Email = u.UserSignupEmail,
                    PhoneNumber = p?.PhoneNumber ?? string.Empty,
                    Country = p?.Country ?? string.Empty,
                    Description = p?.Description ?? string.Empty,
                    UserTitle = p?.UserTitle ?? string.Empty,
                    Bio = p?.Bio ?? string.Empty,
                    AvailableForWork = p?.AvailableForWork,
                    OfferingWork = p?.OfferingWork,
                    DisplayUserName = p?.UsernameDisplay,
                    HidePhoneNumber = p?.HidePhoneNumber,
                    //ProfileImagePath = p?.ProfilePictureFilepath ?? string.Empty,
                    //*Moïse | return api route instead of direct /image so CloudFront routes to backend on EC2 properly
                    ProfileImagePath = BuildProfileImageApiPath(p?.ProfilePictureFilepath) ?? string.Empty,
                    Socials = new Dictionary<string, string?> {
                            { "facebook", s?.FacebookLink },
                            { "Social Email", s?.UserSocialEmailLink },
                            { "X", s?.XLink },
                            { "Github", s?.GithubLink },
                            { "LinkedIn", s?.LinkedinLink }
                    },
                    Ratings = new Dictionary<string, decimal> {
                        { "Time Management", ar?.TimeManagement ?? 0m},
                        { "Payment Reliability", ar?.PaymentReliability ?? 0m},
                        { "Communication", ar?.Communication ?? 0m},
                        { "Collaboration", ar?.Collaboration ?? 0m},
                        { "Recommendation", ar?.Recommendation ?? 0m},
                        { "Total Review Score", ar?.TotalReviewScore ?? 0m}
                    },
                    Skills = sk?.Skills ?? new List<string>()
                };

            return Ok(response);
        }

        //-----------------------
        //Upload Profile File - used by Frontend to upload profile specific files (e.g., profile picture)
        //Ensure that file upload associated to profile is valid and token user is authorized to make changes
        //-----------------------
        [Authorize]
        [HttpPut("UploadFile")]
        public async Task<IActionResult> UploadFile([FromForm] ProfileUploadDto dto)
        {
            //Get the authenticated user ID from JWT token claim
            var authenticatedUserId = GetAuthenticatedUserId();
            if (authenticatedUserId == null)
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            //Verify the authenticated user is trying to get their own details
            if (authenticatedUserId.Value != dto.Id)
            {
                return Forbid(); // 403 Forbidden - user is authenticated but attempting to retrieve the details of another user
            }

            //Ensure that user and profile exist
            var user = await _context.UserAccounts.FindAsync(dto.Id);
            if (user == null) return NotFound("User not found");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(x => x.UserAccountId == dto.Id);
            if (profile == null) return NotFound("Profile not found");

            //Check if file exists
            if (dto.File.Length == 0) return BadRequest("No file uploaded");

            //Define path to save file
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            //If directory doesnt exist, create directory for file storage
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);


            //Get extension from file type meta data
            string extension = string.Empty;
            switch(dto.File.ContentType)
            {
                case "image/jpeg":
                    extension = ".jpg";
                    break;
                case "image/png":
                    extension = ".png";
                    break;
                case "image/gif":
                    extension = ".gif";
                    break;
                case "image/webp":
                    extension = ".webp";
                    break;
                default:
                    return BadRequest("File data malformed");
            }    

            //Generate unique filename - Security risk if we use user provided filename (prevents collisions and malicious attempts to save file outside of chosen directory (example: "../../../.jpg")
            var fileName = $"{Guid.NewGuid()}{extension}";
            //Combine filepath and new file name
            var filePath = Path.Combine(folderPath, fileName);

            //relative path for file "/images/filename.jpg"
            var dbRelativePath = $"/images/{fileName}";
            //*Moïse | api path so images load through /api CloudFront behavior
            var apiRelativePath = BuildProfileImageApiPath(dbRelativePath);

            //Use FileStream to save file to image directory
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }

            //Update existing filepath and extension if such exists, else create new one
            var updateFile = await _context.UserProfiles.FirstOrDefaultAsync(x => x.UserAccountId == dto.Id);
            if (updateFile != null)
            {
                updateFile.ProfilePictureFilepath = dbRelativePath;
                updateFile.ProfilePictureExtension = extension;
            }else
            {
                var newFile = new UserFile
                {
                    FilePath = dbRelativePath,
                    Extension = extension,
                    UserAccountId = dto.Id,
                    UserAccount = user
                };
            }
            
            //Try to update database -- if unsuccessful return error
            try {
                await _context.SaveChangesAsync();
            } catch(DbUpdateException e) {
                return Problem("System error occured. User Profile Update Failed."+e.Message);
            }


            //return Ok(new {path = dbRelativePath});
            //*Moïse | return api route for frontend rendering
            return Ok(new {path = apiRelativePath ?? dbRelativePath});
        }
        
        //Moïse | image endpoint under /api for CloudFront api behavior compatibility
        //can allow anonymous access as user is already authenticated, token user id is checked
        //[Authorize]
        [AllowAnonymous]
        [HttpGet("Image/{fileName}")]
        public IActionResult GetProfileImage([FromRoute] string fileName)
        {
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            var safeFileName = Path.GetFileName(fileName);
            //*Moïse | combine images path with to get full path to file on server
            var fullPath = Path.Combine(imagesPath, safeFileName);

            if(!System.IO.File.Exists(fullPath))
            {
                return NotFound("Image not found");
            }
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullPath, contentType);
        }
        //-----------------------
        //Helper method to retrieve JWT token claim and check if user ID matches JWT sub (user ID)
        //Returns null if claim could not be found or if invalid
        //-----------------------
        private int? GetAuthenticatedUserId()
        {
            //Retrieve current user from http context - bearer token authentication
            var principal = HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null; //no bearer token is currently provided - return null for user id
            }

            //The "sub" (subject) claim contains the user ID
            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                //sub claim not found in token
                return null;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            //Unable to parse user ID from claim
            return null;
        }
    }
}