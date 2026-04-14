using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ContractDevApi.Models;
using ContractDevApi.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
//*Moïse | import ASP.Net Core package for handling forwarded headers
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var connectionString=builder.Configuration.GetConnectionString("DefaultConnection");
//*Moïse | using environment variable for increased security while deploying,no longer stored as plain text in appsettings.json
var dbPassword=Environment.GetEnvironmentVariable("DB_PASSWORD");

//*Moïse | error check in case there is an issue with environmet variables
if(string.IsNullOrEmpty(dbPassword))
    throw new Exception("DB_PASSWORD environment variable is not set");
//*Moïse | replace placeholder in connection string with password from environment variable stored in dbPassowrd  
connectionString=connectionString.Replace("_DB_PASSWORD",dbPassword!);
/*//Database Connection. NOTE: Ensure user_secrets is properly configured to prevent leaking passwords(this is for connecting back end)
builder.Services.AddDbContext<ContractDevContext>(options => 
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());*/

//*Moïse | database conection is now using environment variable so password is not stored as plain text enhancing security
builder.Services.AddDbContext<ContractDevContext>(options => 
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

// CORS for Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
            .WithOrigins(
                //"http://localhost:4200",  // Angular dev server
                //"https://localhost:4200",
                /* *Moïse"http://localhost:5000",  // API (DEVELOPMENT ONLY)
                "https://localhost:5001",*/
                "https://d5tgf2sjo9ckk.cloudfront.net" //*Moïse | CloudFront url, frontend is behind CloudFront distribution
            )
            .AllowAnyHeader()  // Includes Authorization header for JWT
            .AllowAnyMethod());
});
/*
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
            .WithOrigins(
                "http://localhost:4200",  // Angular dev server
                "https://localhost:4200",
                "http://localhost:5000",  // API (DEVELOPMENT ONLY)
                "https://localhost:5001",
            )
            .AllowAnyHeader()  // Includes Authorization header for JWT
            .AllowAnyMethod()
            .AllowCredentials());
});
*/
// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// -------------------------------------------------------------
// JWT AUTHENTICATION CONFIGURATION
// -------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Disable default claim type mapping to use standard JWT claim names (sub, email, etc.)
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

//*Moïse | jwt key is fetched from an environment variable for increased security during deployment, no longer stored as plaintext in appsettings.json
var jwtKey=Environment.GetEnvironmentVariable("JWT_KEY");

//*Moïse | error check in case jwt key environment variable is not set or there is an issue
if(string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT_KEY environment variable is not set");

builder.Services.AddAuthentication(options =>
{
    // Set JWT Bearer as the default authentication scheme
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Disable inbound claim mapping to use standard JWT claim names
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
             //Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
            //*Moïse | jwt key is fetched from environment variable for increased security, not stored as plain text in appsettings.json
            Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

//Swagger - Configure for JWT Bearer Authentication
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    //Define the Bearer security scheme
    //Default settings for registering Swagger to utilize JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT Token"
    });

    // Apply security requirement globally using OpenApiSecuritySchemeReference
    c.AddSecurityRequirement((document) => 
    {
        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document, null);
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(schemeRef, new List<string>());
        return requirement;
    });
});

// Register JwtService
builder.Services.AddScoped<JwtService>();

var app = builder.Build();

//Allow storing static files - from wwwroot default
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

//*Moïse | added to handle forwarded headers when behind CloudFront distribution ensuring correct ip and protocals are recognised
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
//*Moïse | https handled by CloudFront distribution
//app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//Root test endpoint
//Allows for quickly checking if api is running
app.MapGet("/", () => "API is running...");

//*Moïse | Apply any pending migrations to RDS database on startup during deployment. Seed skills table required to update profile skills when application is deployed
using(var scope=app.Services.CreateScope())
{   // apply database migrations to RDS on startup
    var db=scope.ServiceProvider.GetRequiredService<ContractDevContext>();
    db.Database.Migrate();
    if(!db.Skills.Any())
    {
        db.Skills.AddRange(new List<Skill>
        {   
            new Skill {SkillName = "HTML" },
            new Skill {SkillName = "css" },
            new Skill {SkillName = "JavaScript" },
            new Skill {SkillName = "Angular" },
            new Skill {SkillName = "React" },
            new Skill {SkillName = "Bootstrap" },
            new Skill {SkillName = "PostgreSQL" },
            new Skill {SkillName = "MySQL" },
            new Skill {SkillName = "MongoDB" },
            new Skill {SkillName = "AWS" },
            new Skill {SkillName = "Docker" },
            new Skill {SkillName = "Java" },
            new Skill {SkillName = "Python" },
            new Skill {SkillName = "C#" },
            new Skill {SkillName = "C++" },
            new Skill {SkillName = "C" },
            new Skill {SkillName = "Rust" },
            new Skill {SkillName = "Go" },
            new Skill {SkillName = "TypeScript" },
            new Skill {SkillName = "Frontend" },
            new Skill {SkillName = "Backend" },
            new Skill {SkillName = "Cloud Developer" },
            new Skill {SkillName = "Full Stack" },
            new Skill {SkillName = "Mobile Developer" },
            new Skill {SkillName = "Database Developer" },
            new Skill {SkillName = "Web Developer" },

        });
            db.SaveChanges();
        }
}

app.Run();
