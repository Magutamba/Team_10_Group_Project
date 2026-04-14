// These using statements pull in the classes we need to work with JWTs,
// claims, cryptography, and text encoding.
using System.IdentityModel.Tokens.Jwt;   // For creating and handling JWT tokens
using System.Security.Claims;            // For representing user identity as claims
using Microsoft.IdentityModel.Tokens;    // For security keys, signing credentials, token validation
using System.Text;                       // For encoding strings into byte arrays
using ContractDevApi.DTOs;                  // Your User model (adjust namespace if needed)

namespace ContractDevApi.Services
{
    // JwtService is a reusable class responsible for generating JWT tokens
    // for authenticated users. It does NOT handle HTTP or controllers directly.
    public class JwtService
    {
        // IConfiguration lets us read values from appsettings.json (like Jwt:Key, Jwt:Issuer, etc.)
        private readonly IConfiguration _config;

        // The constructor receives IConfiguration via dependency injection.
        // This is wired up in Program.cs with: builder.Services.AddScoped<JwtService>();
        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        // This method generates a signed JWT token for a given User.
        // The token will contain claims (user info) and an expiration time.
        public string GenerateToken(UserResponseDto user)
        {
            // 1. Define the claims that will be embedded in the token.
            //    Claims are pieces of information about the user.
            //    These are what the backend can later read from the token.
            var claims = new[]
            {
                // "sub" (subject) is a standard JWT claim that usually holds the user ID.
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),

                // "email" is another standard claim. We store the user's email here.
                new Claim(JwtRegisteredClaimNames.Email, user.Email),

                // A custom claim "username". This is not required by JWT spec,
                // but useful for your app. If Username is null, we use an empty string.
                new Claim("username", user.Username ?? "")
            };

            // 2. Create the signing key.
            //    This key is used to sign the token so the server can later verify
            //    that the token was not tampered with.
            //
            //    We read the secret key from appsettings.json: "Jwt:Key".
            //    It must be a sufficiently long, random string in production.
            // var key = new SymmetricSecurityKey(
            //     Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
            // );

            // // 3. Create signing credentials.
            // //    This tells JWT which algorithm to use to sign the token.
            // //    Here we use HMAC-SHA256, a common and secure choice.
            // var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            //*Moïse | old direct fallback kept for traceability
            // var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? _config["Jwt:Key"];
            //*Moïse | jwt key is fetched from environment variable in EC2 during deployment for security
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException("JWT signing key not configured.");
            }
            //*Moïse
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            // 4. Build the actual JWT token object.
            //    We pass:
            //      - issuer: who created the token (from appsettings: Jwt:Issuer)
            //      - audience: who the token is intended for (Jwt:Audience)
            //      - claims: the user info we defined above
            //      - expires: when the token should stop being valid
            //      - signingCredentials: how the token is signed
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_config["Jwt:ExpiresInMinutes"]!)
                ),
                signingCredentials: creds
            );

            // 5. Convert the JwtSecurityToken object into a compact string.
            //    This is the actual token you send back to Angular, e.g.:
            //    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}