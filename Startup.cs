using IdentityMongo.Interface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using IdentityMongo.Settings;
using IdentityMongo.Models;
//using Microsoft.AspNetCore.Identity.UI.Services;
using IdentityMongo.Services;
using Microsoft.AspNetCore.Identity;
using MongoDbGenericRepository;


namespace IdentityMongo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var mongoDbSettings = Configuration.GetSection(nameof(MongoDbConfig)).Get<MongoDbConfig>();

            //var mongodbSettingsRes = Configuration.GetSection(nameof(MongoDbReservations)).Get<MongoDbReservations>();
            //services.Configure<MongoDbSettings>(Configuration.GetSection("MongoDB"));
            //services.AddSingleton<MongoDbService>();

            services.AddIdentity<ApplicationUser, ApplicationRole>(options => {
                options.SignIn.RequireConfirmedAccount = true;
                
                })
                .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>
                (
                    mongoDbSettings.ConnectionString, mongoDbSettings.Name
                )
                .AddDefaultTokenProviders();

            services.Configure<MongoDbSettings>(Configuration.GetSection("MongoDBReservations"));
            services.AddSingleton<MongoDbReservationService>();

            services.Configure<SmtpSettings>(Configuration.GetSection("SmtpSettings"));
            services.AddControllersWithViews();
            services.AddSingleton<IEmailSender, EmailSenderService>();
            //services.AddTransient<IEmailSender, EmailSender>();
            //services.Configure<AuthMessageSenderOptions>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }



            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
            
        }
    }
}
