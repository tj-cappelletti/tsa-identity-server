using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdentityServer4;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsa.IdentityServer.Web.Configuration;
using Tsa.IdentityServer.Web.DataContexts;

namespace Tsa.IdentityServer.Web
{
    public class Startup
    {
        private const string CreateDatabaseArgumentKey = "createDb";
        private const string SeedDatabaseSourceKey = "dbSeedSource";

        public IConfiguration Configuration { get; set; }

        public IWebHostEnvironment WebHostEnvironment { get; set; }

        public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            Configuration = configuration;
            WebHostEnvironment = webHostEnvironment;
        }

        private static void AddLocalIdentityServerApiResources(ConfigurationDbContext configurationDbContext, ILogger logger)
        {
            var apiResources = new List<ApiResource>
            {
                new("tsa.submissions", "TSA Submissions API")
                {
                    Scopes = { "tsa.submissions.read", "tsa.submissions.create" }
                },
                new("tsa.coding.submissions", "TSA Coding Submissions API")
                {
                    Scopes = { "tsa.coding.submissions.read", "tsa.coding.submissions.create" }
                }
            };

            foreach (var apiResource in apiResources.Where(apiScope => !configurationDbContext.ApiResources.Any(a => a.Name == apiScope.Name)))
            {
                logger.LogInformation("Creating the API resource {apiResource}", apiResource.Name);
                configurationDbContext.ApiResources.Add(apiResource.ToEntity());
            }
        }

        private static void AddLocalIdentityServerApiScopes(ConfigurationDbContext configurationDbContext, ILogger logger)
        {
            var apiScopes = new List<ApiScope>
            {
                new("tsa.submissions.read", "Display/read submissions"),
                new("tsa.submissions.create", "Create submissions"),
                new("tsa.coding.submissions.read", "Display/read coding submissions"),
                new("tsa.coding.submissions.create", "Create coding submissions")
            };

            foreach (var apiScope in apiScopes.Where(apiScope => !configurationDbContext.ApiScopes.Any(a => a.Name == apiScope.Name)))
            {
                logger.LogInformation("Creating the API scope {apiScope}", apiScope.Name);
                configurationDbContext.ApiScopes.Add(apiScope.ToEntity());
            }
        }

        private static void AddLocalIdentityServerClients(ConfigurationDbContext configurationDbContext, ILogger logger)
        {
            var clients = new List<Client>
            {
                new()
                {
                    AllowOfflineAccess = false,
                    AllowPlainTextPkce = false,
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        "tsa.coding.submissions.read",
                        "tsa.coding.submissions.create"
                    },
                    ClientId = "tsa.coding.submissions.web",
                    ClientName = "TSA Coding Submissions Web UI",
                    ClientSecrets = { new Secret("a673bbae-71e4-4962-a623-665689c4dd34".Sha256()) },
                    PostLogoutRedirectUris = { "https://localhost:44345/signout-oidc" },
                    RedirectUris = { "https://localhost:44345/signin-oidc" },
                    RequireConsent = false,
                    RequirePkce = true
                },
                new()
                {
                    AllowOfflineAccess = false,
                    AllowPlainTextPkce = false,
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        "submissions.read",
                        "submissions.create"
                    },
                    ClientId = "tsa.submissions.web",
                    ClientName = "TSA Submissions Web UI",
                    ClientSecrets = { new Secret("945931d5-6100-4129-b2c0-d9e9b34d1828".Sha256()) },
                    RequireConsent = false,
                    RequirePkce = true
                }
            };

            foreach (var client in clients.Where(apiScope => !configurationDbContext.Clients.Any(c => c.ClientId == apiScope.ClientId)))
            {
                logger.LogInformation("Creating the client {client}", client.ClientId);
                configurationDbContext.Clients.Add(client.ToEntity());
            }
        }

        private static void AddLocalIdentityServerConfiguration(IApplicationBuilder app)
        {
            // ReSharper disable once PossibleNullReferenceException
            // If the IServiceScopeFactory is null here, let the
            // exception bubble up as this method isn't being called
            // at the right time
            using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();
            var configurationDbContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

            var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
            
            if (loggerFactory == null) throw new NullReferenceException($"The `{nameof(loggerFactory)}` was null");

            var logger = loggerFactory.CreateLogger("Tsa.IdentityServer.Web.Startup");

            //TODO: Move to a configuration file
            AddLocalIdentityServerApiScopes(configurationDbContext, logger);
            AddLocalIdentityServerApiResources(configurationDbContext, logger);
            AddLocalIdentityServerClients(configurationDbContext, logger);

            configurationDbContext.SaveChanges();
        }

        public static void AddLocalIdentityServerUsers(IApplicationBuilder app)
        {
            // ReSharper disable once PossibleNullReferenceException
            // If the IServiceScopeFactory is null here, let the
            // exception bubble up as this method isn't being called
            // at the right time
            using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();

            var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();

            if (loggerFactory == null) throw new NullReferenceException($"The `{nameof(loggerFactory)}` was null");

            var logger = loggerFactory.CreateLogger("Tsa.IdentityServer.Web.Startup");

            CreateLocalIdentityServerRole(roleManager, "judge", logger);
            CreateLocalIdentityServerRole(roleManager, "participant", logger);

            var studentOneIdentityUser = new IdentityUser
            {
                UserName = "9999-001",
                Email = "student1@tsa.local",
                EmailConfirmed = true,
                Id = "9999-001"
            };

            var studentTwoIdentityUser = new IdentityUser
            {
                UserName = "9999-002",
                Email = "student2@tsa.local",
                EmailConfirmed = true,
                Id = "9999-002"
            };

            CreateLocalIdentityServerUser(userManager, studentOneIdentityUser, "participant", logger);
            CreateLocalIdentityServerUser(userManager, studentTwoIdentityUser, "participant", logger);

            var judgeOneIdentityUser = new IdentityUser
            {
                UserName = "judge01",
                Email = "judge01@tsa.local",
                EmailConfirmed = true,
                Id = "judge01"
            };

            var judgeTwoIdentityUser = new IdentityUser
            {
                UserName = "judge02",
                Email = "judge02@tsa.local",
                EmailConfirmed = true,
                Id = "judge02"
            };

            CreateLocalIdentityServerUser(userManager, judgeOneIdentityUser, "judge", logger);
            CreateLocalIdentityServerUser(userManager, judgeTwoIdentityUser, "judge", logger);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            InitializeDatabase(app);

            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseIdentityServer();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var identityServerConnectionString = Configuration.GetConnectionString("TsaIdentityServer");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<TsaIdentityDbContext>(options => options.UseSqlServer(identityServerConnectionString));

            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<TsaIdentityDbContext>();

            //TODO: Add logic to detect when to use Developer signing and when not
            services.AddIdentityServer()
                .AddAspNetIdentity<IdentityUser>()
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        builder.UseSqlServer(identityServerConnectionString,
                            sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly));
                })
                .AddDeveloperSigningCredential()
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        builder.UseSqlServer(identityServerConnectionString,
                            sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly));
                });

            services.AddControllersWithViews();
        }

        private static void CreateLocalIdentityServerRole(RoleManager<IdentityRole> roleManager, string role, ILogger logger)
        {
            if (roleManager.RoleExistsAsync(role).Result) return;

            logger.LogInformation("Creating the role {role}", role);

            var identityResult = roleManager.CreateAsync(new IdentityRole(role)).Result;

            if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
        }

        private static void CreateLocalIdentityServerUser(UserManager<IdentityUser> userManager, IdentityUser identityUser, string role, ILogger logger)
        {
            if (userManager.FindByIdAsync(identityUser.Id).Result != null) return;

            logger.LogInformation("Creating the user {identityUser}", identityUser.Id);

            var identityResult = userManager.CreateAsync(identityUser, "Pa$$w0rd").Result;

            if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);

            logger.LogInformation("Assigning the user {identityUser} to the role {role}", identityUser.Id, role);
            identityResult = userManager.AddToRoleAsync(identityUser, role).Result;

            if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
        }

        private void InitializeDatabase(IApplicationBuilder app)
        {
            var createDb = Configuration.GetValue<bool>(CreateDatabaseArgumentKey);
            if (createDb) ApplyDatabaseMigrations(app);

            var databaseSeedSourceValue = Configuration[SeedDatabaseSourceKey];
            DatabaseSeedSource databaseSeedSource;

            if (string.IsNullOrWhiteSpace(databaseSeedSourceValue))
                databaseSeedSource = DatabaseSeedSource.None;
            else
                databaseSeedSource = databaseSeedSourceValue.ToLower() switch
                {
                    "local" => DatabaseSeedSource.Local,
                    _ => DatabaseSeedSource.None
                };

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            // There is nothing to do when the source is none
            // In a default case, do nothing and assume it is a second instance
            // that needs to connect to an existing database
            switch (databaseSeedSource)
            {
                case DatabaseSeedSource.Local:
                    AddLocalIdentityServerConfiguration(app);
                    AddLocalIdentityServerUsers(app);
                    break;

                case DatabaseSeedSource.LocalStorage:
                    throw new NotImplementedException();

                case DatabaseSeedSource.AzureStorage:
                    throw new NotImplementedException();
            }
        }

        private static void ApplyDatabaseMigrations(IApplicationBuilder app)
        {
            // ReSharper disable once PossibleNullReferenceException
            // If the IServiceScopeFactory is null here, let the
            // exception bubble up as this method isn't being called
            // at the right time
            using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();

            var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();

            if (loggerFactory == null) throw new NullReferenceException($"The `{nameof(loggerFactory)}` was null");

            var logger = loggerFactory.CreateLogger("Tsa.IdentityServer.Web.Startup");

            logger.LogInformation("Running database migration for the {dbContext}", "PersistedGrantDbContext");
            serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

            logger.LogInformation("Running database migration for the {dbContext}", "ConfigurationDbContext");
            serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.Migrate();

            logger.LogInformation("Running database migration for the {dbContext}", "TsaIdentityDbContext");
            serviceScope.ServiceProvider.GetRequiredService<TsaIdentityDbContext>().Database.Migrate();
        }
    }
}
