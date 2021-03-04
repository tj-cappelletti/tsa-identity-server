using System.Collections.Generic;
using System.Security.Claims;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tsa.IdentityServer.Web
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            //TODO: Add logic to detect when to use Developer signing and when not

            services.AddIdentityServer()
                .AddDeveloperSigningCredential()
                .AddInMemoryApiResources(GetApiResources())
                .AddInMemoryApiScopes(GetApiScopes())
                .AddInMemoryClients(GetClients())
                .AddInMemoryIdentityResources(GetIdentityResources())
                .AddTestUsers(GetTestUsers());

            services.AddControllersWithViews();
        }

#if DEBUG
        //TODO: Improve configuration to allows for injection of test data (i.e., container startup)
        private static List<ApiScope> GetApiScopes()
        {
            return new()
            {
                new ApiScope("tsa.submissions.read", "Display/read submissions"),
                new ApiScope("tsa.submissions.create", "Create submissions"),
                new ApiScope("tsa.coding.submissions.read", "Display/read coding submissions"),
                new ApiScope("tsa.coding.submissions.create", "Create coding submissions")
            };
        }

        private static List<ApiResource> GetApiResources()
        {
            return new()
            {
                new ApiResource("tsa.submissions", "TSA Submissions API")
                {
                    Scopes = { "tsa.submissions.read", "tsa.submissions.create" }
                },
                new ApiResource("tsa.coding.submissions", "TSA Coding Submissions API")
                {
                    Scopes = { "tsa.coding.submissions.read", "tsa.coding.submissions.create" }
                }
            };
        }

        private static List<Client> GetClients()
        {
            return new()
            {
                new Client
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
                },
                new Client
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
                }
            };
        }

        private static List<IdentityResource> GetIdentityResources()
        {
            return new()
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResource
                {
                    Name = "role",
                    UserClaims = new List<string> { "role" }
                }
            };
        }

        private static List<TestUser> GetTestUsers()
        {
            return new()
            {
                new TestUser
                {
                    Claims =
                    {
                        new Claim(JwtClaimTypes.Name, "Test Judge One"),
                        new Claim(JwtClaimTypes.GivenName, "Test"),
                        new Claim(JwtClaimTypes.FamilyName, "Judge"),
                        new Claim(JwtClaimTypes.Email, "test_judge_one@tsa.local"),
                        new Claim(JwtClaimTypes.Role, "judge")
                    },
                    Password = "judge1",
                    SubjectId = "judge01",
                    Username = "judge01"
                },
                new TestUser
                {
                    Claims =
                    {
                        new Claim(JwtClaimTypes.Name, "Test Judge Two"),
                        new Claim(JwtClaimTypes.GivenName, "Test"),
                        new Claim(JwtClaimTypes.FamilyName, "Judge"),
                        new Claim(JwtClaimTypes.Email, "test_judge_two@tsa.local"),
                        new Claim(JwtClaimTypes.Role, "judge")
                    },
                    Password = "judge2",
                    SubjectId = "judge02",
                    Username = "judge02"
                },
                new TestUser
                {
                    Claims =
                    {
                        new Claim(JwtClaimTypes.Name, "Test Participant One"),
                        new Claim(JwtClaimTypes.GivenName, "Test"),
                        new Claim(JwtClaimTypes.FamilyName, "Participant"),
                        new Claim(JwtClaimTypes.Email, "test_participant_one@tsa.local"),
                        new Claim(JwtClaimTypes.Role, "participant")
                    },
                    Password = "participant1",
                    SubjectId = "9999-001",
                    Username = "9999-001"
                },
                new TestUser
                {
                    Claims =
                    {
                        new Claim(JwtClaimTypes.Name, "Test Participant Two"),
                        new Claim(JwtClaimTypes.GivenName, "Test"),
                        new Claim(JwtClaimTypes.FamilyName, "Participant"),
                        new Claim(JwtClaimTypes.Email, "test_participant_two@tsa.local"),
                        new Claim(JwtClaimTypes.Role, "participant")
                    },
                    Password = "participant1",
                    SubjectId = "9999-002",
                    Username = "9999-002"
                }
            };
        }
#endif
    }
}
