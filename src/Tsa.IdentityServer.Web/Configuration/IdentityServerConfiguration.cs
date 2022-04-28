using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Entities;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsa.IdentityServer.Web.DataContexts;
using ApiResource = IdentityServer4.Models.ApiResource;
using ApiScope = IdentityServer4.Models.ApiScope;
using Client = IdentityServer4.Models.Client;
using IdentityResource = IdentityServer4.Models.IdentityResource;
using Secret = IdentityServer4.Models.Secret;

namespace Tsa.IdentityServer.Web.Configuration
{
    public sealed class IdentityServerConfiguration : IDisposable
    {
        private readonly ConfigurationDbContext _configurationDbContext;
        private readonly string _configurationLocation;
        private readonly ConfigurationSource _configurationSource;
        private readonly ILogger _logger;
        private readonly PersistedGrantDbContext _persistedGrantDbContext;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly TsaIdentityDbContext _tsaIdentityDbContext;
        private readonly UserManager<IdentityUser> _userManager;

        public IdentityServerConfiguration(IApplicationBuilder applicationBuilder, ConfigurationSource configurationSource, string configurationLocation)
        {
            _configurationLocation = configurationLocation;
            _configurationSource = configurationSource;

            // ReSharper disable once PossibleNullReferenceException
            // If the IServiceScopeFactory is null here, let the
            // exception bubble up as this method isn't being called
            // at the right time
            var serviceScope = applicationBuilder.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();

            _configurationDbContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
            _persistedGrantDbContext = serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
            _roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            _tsaIdentityDbContext = serviceScope.ServiceProvider.GetRequiredService<TsaIdentityDbContext>();
            _userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var loggerFactory = applicationBuilder.ApplicationServices.GetService<ILoggerFactory>();
            // ReSharper disable once PossibleNullReferenceException
            // If loggerFactory is null, this was called at the wrong time
            // hence, let it die with a NullReferenceException
            _logger = loggerFactory.CreateLogger("Tsa.IdentityServer.Web.IdentityServerConfiguration");
        }

        private void AddIdentityServerApiResources(IEnumerable<ApiResource> apiResources)
        {
            foreach (var apiResource in apiResources)
            {
                var apiResourceEntity = _configurationDbContext.ApiResources.SingleOrDefault(ar => ar.Name == apiResource.Name);

                if (apiResourceEntity == null)
                {
                    _logger.LogInformation("Creating IdentityServer API Resource {apiResource}", apiResource.Name);
                    apiResourceEntity = _configurationDbContext.ApiResources.Add(apiResource.ToEntity()).Entity;
                }

                if (apiResourceEntity.Scopes.All(ars => ars.Scope != "role"))
                    apiResourceEntity.Scopes.Add(new ApiResourceScope { ApiResourceId = apiResourceEntity.Id, Scope = "role" });

                if (apiResourceEntity.Scopes.All(ars => ars.Scope != "email"))
                    apiResourceEntity.Scopes.Add(new ApiResourceScope { ApiResourceId = apiResourceEntity.Id, Scope = "email" });

                if (apiResourceEntity.Scopes.All(ars => ars.Scope != "profile"))
                    apiResourceEntity.Scopes.Add(new ApiResourceScope { ApiResourceId = apiResourceEntity.Id, Scope = "profile" });

                if (apiResourceEntity.Scopes.All(ars => ars.Scope != "openid"))
                    apiResourceEntity.Scopes.Add(new ApiResourceScope { ApiResourceId = apiResourceEntity.Id, Scope = "openid" });
            }
        }

        private void AddIdentityServerApiScopes(IEnumerable<ApiScope> apiScopes)
        {
            foreach (var apiScope in apiScopes)
            {
                if (_configurationDbContext.ApiScopes.Any(a => a.Name == apiScope.Name)) continue;

                _logger.LogInformation("Creating IdentityServer API Scope {apiScopes}", apiScope.Name);

                _configurationDbContext.ApiScopes.Add(apiScope.ToEntity());
            }
        }

        private void AddIdentityServerClients(IEnumerable<Client> clients)
        {
            foreach (var client in clients)
            {
                if (_configurationDbContext.Clients.Any(c => c.ClientName == client.ClientName)) continue;

                var secrets = client.ClientSecrets.Select(s => s.Description).ToList();

                client.ClientSecrets.Clear();

                foreach (var secret in secrets) client.ClientSecrets.Add(new Secret(secret.Sha256()));

                _logger.LogInformation("Creating IdentityServer Client {client}", client.ClientName);

                _configurationDbContext.Clients.Add(client.ToEntity());
            }

            _configurationDbContext.SaveChanges();
        }

        private void AddIdentityServerIdentityResources()
        {
            var openIdIdentityResource = new IdentityResources.OpenId().ToEntity();
            var profileIdentityResource = new IdentityResources.Profile().ToEntity();
            var emailIdentityResource = new IdentityResources.Email().ToEntity();
            var roleIdentityResource = new IdentityResource("role", new[] { "role" }).ToEntity();

            if (!_configurationDbContext.IdentityResources.Any(ir => ir.Name == openIdIdentityResource.Name))
                _configurationDbContext.IdentityResources.Add(openIdIdentityResource);

            if (!_configurationDbContext.IdentityResources.Any(ir => ir.Name == profileIdentityResource.Name))
                _configurationDbContext.IdentityResources.Add(profileIdentityResource);

            if (!_configurationDbContext.IdentityResources.Any(ir => ir.Name == emailIdentityResource.Name))
                _configurationDbContext.IdentityResources.Add(emailIdentityResource);

            if (!_configurationDbContext.IdentityResources.Any(ir => ir.Name == openIdIdentityResource.Name))
                _configurationDbContext.IdentityResources.Add(roleIdentityResource);

            _configurationDbContext.SaveChanges();
        }

        private void AddIdentityServerRoles(IEnumerable<string> roles)
        {
            foreach (var role in roles)
            {
                var identityRole = _roleManager.FindByNameAsync(role).Result;

                if (identityRole != null) continue;

                _logger.LogInformation("Creating the role {role}", role);

                var identityResult = _roleManager.CreateAsync(new IdentityRole(role)).Result;

                if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
            }
        }

        private void AddIdentityServerUserRoles(IEnumerable<IdentityUserRole> identityUserRoles)
        {
            foreach (var identityUserRole in identityUserRoles)
            {
                var identityUsers = _userManager.GetUsersInRoleAsync(identityUserRole.RoleName).Result;

                if (identityUsers.Any(iu => iu.Id == identityUserRole.UserId)) continue;

                var identityUser = _userManager.FindByIdAsync(identityUserRole.UserId).Result;

                if (identityUser == null) throw new NullReferenceException("Could not find user while assigning them to the role.");

                _logger.LogInformation("Assigning the user {identityUser} to the role {role}", identityUser.Id, identityUserRole.RoleName);

                var identityResult = _userManager.AddToRoleAsync(identityUser, identityUserRole.RoleName).Result;

                if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
            }
        }

        private void AddIdentityServerUsers(IEnumerable<IdentityUser> identityUsers)
        {
            foreach (var identityUser in identityUsers)
            {
                if (_userManager.FindByIdAsync(identityUser.Id).Result != null) continue;

                var password = identityUser.PasswordHash;
                identityUser.PasswordHash = string.Empty;

                _logger.LogInformation("Creating the user {identityUser}", identityUser.Id);

                var identityResult = _userManager.CreateAsync(identityUser, password).Result;

                if (!identityResult.Succeeded) throw new Exception(identityResult.Errors.First().Description);
            }
        }

        public void ApplyDatabaseMigrations()
        {
            _logger.LogInformation("Running database migration for the {dbContext}", "PersistedGrantDbContext");
            _persistedGrantDbContext.Database.Migrate();

            _logger.LogInformation("Running database migration for the {dbContext}", "ConfigurationDbContext");
            _configurationDbContext.Database.Migrate();

            _logger.LogInformation("Running database migration for the {dbContext}", "TsaIdentityDbContext");
            _tsaIdentityDbContext.Database.Migrate();
        }

        public void Dispose()
        {
            _configurationDbContext.Dispose();
            _persistedGrantDbContext.Dispose();
            _roleManager.Dispose();
            _tsaIdentityDbContext.Dispose();
            _userManager.Dispose();
        }

        public void LoadConfiguration()
        {
            _logger.LogInformation("Loading configuration from: {configSource}", _configurationSource);
            string identityServerSeedDataJson;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            // There is nothing to do when the source is none
            // In a default case, do nothing and assume it is a second instance
            // that needs to connect to an existing database
            switch (_configurationSource)
            {
#if DEBUG
                // Project should only be available when the DEBUG flag is set
                // This is to help safeguard production from having testing
                // settings and credentials published
                case ConfigurationSource.Project:
                    identityServerSeedDataJson = File.ReadAllText("identityserverseeddata.json");
                    break;
#endif
                case ConfigurationSource.SystemStorage:
                    identityServerSeedDataJson = File.ReadAllText(_configurationLocation);
                    break;

                case ConfigurationSource.AzureStorage:
                    throw new NotImplementedException();

                default:
                    return;
            }

            if (string.IsNullOrWhiteSpace(identityServerSeedDataJson)) return;

            var identityServerSeedData = JsonSerializer.Deserialize<IdentityServerSeedData>(identityServerSeedDataJson);

            if (identityServerSeedData == null) throw new NullReferenceException("Unable to deserialize the seed data file.");

            AddIdentityServerApiResources(identityServerSeedData.ApiResources);
            AddIdentityServerApiScopes(identityServerSeedData.ApiScopes);
            AddIdentityServerClients(identityServerSeedData.Clients);
            AddIdentityServerIdentityResources();
            AddIdentityServerRoles(identityServerSeedData.Roles);
            AddIdentityServerUsers(identityServerSeedData.IdentityUsers);
            AddIdentityServerUserRoles(identityServerSeedData.IdentityUserRoles);
        }

        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        private class IdentityServerSeedData
        {
            public IEnumerable<ApiResource> ApiResources { get; set; }

            public IEnumerable<ApiScope> ApiScopes { get; set; }

            public IEnumerable<Client> Clients { get; set; }

            public IEnumerable<IdentityUserRole> IdentityUserRoles { get; set; }

            public IEnumerable<IdentityUser> IdentityUsers { get; set; }

            public IEnumerable<string> Roles { get; set; }
        }

        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        private class IdentityUserRole
        {
            public string RoleName { get; set; }

            public string UserId { get; set; }
        }
    }
}
