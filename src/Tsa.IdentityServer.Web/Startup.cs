using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tsa.IdentityServer.Web.Configuration;
using Tsa.IdentityServer.Web.DataContexts;

namespace Tsa.IdentityServer.Web
{
    public class Startup
    {
        private const string CreateDatabaseArgumentKey = "CREATE_DB";
        private const string DatabaseSeedLocationKey = "DB_SEED_SOURCE_LOCATION";
        private const string DatabaseSeedSourceKey = "DB_SEED_SOURCE";

        public IConfiguration Configuration { get; set; }

        public IWebHostEnvironment WebHostEnvironment { get; set; }

        public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            Configuration = configuration;
            WebHostEnvironment = webHostEnvironment;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            InitializeDatabase(app);

            if (Configuration["DOCKER_CONTAINER"] != null && Configuration["DOCKER_CONTAINER"] == "Y")
                app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

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

            if (Configuration["DOCKER_CONTAINER"] != null && Configuration["DOCKER_CONTAINER"] == "Y")
                services.AddCors();

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

        private void InitializeDatabase(IApplicationBuilder app)
        {
            var createDb = Configuration.GetValue<bool>(CreateDatabaseArgumentKey);
            var databaseSeedLocation = Configuration[DatabaseSeedLocationKey];
            var databaseSeedSourceValue = Configuration[DatabaseSeedSourceKey];

            ConfigurationSource databaseSeedSource;

            if (string.IsNullOrWhiteSpace(databaseSeedSourceValue))
                databaseSeedSource = ConfigurationSource.None;
            else
                databaseSeedSource = databaseSeedSourceValue.ToLower() switch
                {
                    "project" => ConfigurationSource.Project,
                    "system" => ConfigurationSource.SystemStorage,
                    "azure" => ConfigurationSource.AzureStorage,
                    _ => ConfigurationSource.None
                };

            var identityServerConfiguration = new IdentityServerConfiguration(app, databaseSeedSource, databaseSeedLocation);

            if (createDb) identityServerConfiguration.ApplyDatabaseMigrations();

            identityServerConfiguration.LoadConfiguration();
        }
    }
}
