//* Moïse | design time factory needed so dotnet ef add InitialCreate works with production changes(using RDS not local), and can work without booting
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ContractDevApi.Models
{   
    //ef call this class at desing time for ef add InitialCreate
    public class ContractDevContextFactory : IDesignTimeDbContextFactory<ContractDevContext>
    {
        public ContractDevContext CreateDbContext(string[] args)
       
        {   
            //build configuration from appsettings and environment variables
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            //get connection string from configuration
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is missiong.");

            //get db password from environment variable and replace placeholder with it
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if(!string.IsNullOrEmpty(dbPassword))
            {
                connectionString = connectionString.Replace("_DB_Password", dbPassword);
            }
            
            //configure db context options with connection string and snake case naming convention
            var optionsBuilder = new DbContextOptionsBuilder<ContractDevContext>();
            optionsBuilder.UseNpgsql(connectionString)
                          .UseSnakeCaseNamingConvention();
            //return a design time instance of ContractDevContext with the configured options
            return new ContractDevContext(optionsBuilder.Options);
        }
    }
}
