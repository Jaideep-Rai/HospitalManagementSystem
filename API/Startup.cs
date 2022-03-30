using API.Dbcontext;
using API.UpdateUserDb;
using BAL.DependencyResolver;
using ExceptionHandling.DependencyResolver;
using ExceptionHandling.ExceptionManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NLog;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Text;

namespace API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            LogManager.LoadConfiguration(String.Concat(Directory.GetCurrentDirectory(), "/nlog.config"));
        }

        public IConfiguration Configuration { get; }
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson(options =>
            options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            );// for 3.1
            services.AddCors(options =>
            {
                options.AddPolicy(MyAllowSpecificOrigins,
                builder =>
                {
                    builder.AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod();
                });
            });

            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0); not needed in 3.1
            //services.Configure<EmailSettings>(Configuration.GetSection("EmailSettings"));

            //Dependencies resolver
            services.DIBALResolver();
            services.ExceptionDIResolver();
            services.AddSwaggerGen();
            services.AddDistributedMemoryCache();

            // ===== Add DbContext ========
            services.AddDbContext<ApplicationDbContext>();

            // ===== Add Identity ========

            services.AddIdentity<UserField, IdentityRole>(config =>
            {
                config.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-. _@+";
                //config.SignIn.RequireConfirmedEmail = true;
                //config.User.RequireUniqueEmail = true;
            })


            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
            //===== Add Jwt Authentication ========
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear(); // => remove default claims
            services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

            })
            .AddJwtBearer(cfg =>
            {
                cfg.RequireHttpsMetadata = false;
                cfg.SaveToken = true;
                cfg.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Configuration["JwtIssuer"],
                    ValidAudience = Configuration["JwtIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtKey"])),
                    ClockSkew = TimeSpan.Zero // remove delay of token when expire
                };
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ApplicationDbContext dbContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
            Path.Combine(env.ContentRootPath, "assets")),
                RequestPath = "/assets"
            });

            app.UseSwagger();
            app.UseSwaggerUI(o =>

            {
                o.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic Reporting Tool");
            });
            app.UseCors(MyAllowSpecificOrigins);
            app.ConfigureExceptionMiddleware();
            app.UseHttpsRedirection();
            //app.UseMvc(); not needed in 3.1
            app.UseAuthentication();

            app.UseRouting(); // for 3.1
            app.UseAuthorization(); //for 3.1
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            }); // for 3.1
                // ===== Create tables ======
            dbContext.Database.EnsureCreated();
        }

    }
}