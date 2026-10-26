using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectTripsDB.Models;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Dto.User;
using Service.Interface;
using Service.Services;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;


IdentityModelEventSource.ShowPII = true;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter Token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
{
    throw new InvalidOperationException(
        "Jwt:Key is not set. Run: dotnet user-secrets set \"Jwt:Key\" \"<at least 32 characters>\" --project ProjectTrips");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            )
        };

        // ✅ אירועים מסודרים ללא כפילויות
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                Console.WriteLine("Token received: " + context.Token);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Authentication failed: " + context.Exception);

                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.StatusCode = 401;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully for user: " +
                                  context.Principal.Identity?.Name);
                return Task.CompletedTask;
            }
        };
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("reviews", context =>
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        // אם אין משתמש מחובר – נחסום או ניתן מפתח אנונימי
        if (string.IsNullOrEmpty(userId))
            userId = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }
        );
    });
});

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowReact",
//        policy =>
//        {
//            policy.WithOrigins("http://localhost:5173")
//                  .AllowAnyHeader()
//                  .AllowAnyMethod();
//        });
//});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVitePorts", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin))
                    return false;

                var uri = new Uri(origin);

                return uri.Host == "localhost"
                       && uri.Port.ToString().StartsWith("517");
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<IRegionRepository, RegionRepository>();
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IImageRepository, ImageRepository>();
builder.Services.AddScoped<IDayTripRepository, DayTripRepository>();
builder.Services.AddScoped<IDayTripItemRepository, DayTripItemRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ITypeRepository, TypeRepository>();
builder.Services.AddScoped<ISuggestedStopsRepository, SuggestedStopsRepository>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IRegionService, RegionService>();
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IDayTripService, DayTripService>();
builder.Services.AddScoped<IDayTripItemService, DayTripItemService>();
builder.Services.AddScoped<IDayTripCalculatedFieldsService, DayTripCalculatedFieldsService>();
builder.Services.AddScoped<IRecalculateAllTripsContainingPlaceOrRouteService, RecalculateAllTripsContainingPlaceOrRouteService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ITypeService, TypeService>();
builder.Services.AddScoped<IGetBestTripsService, GetBestTripsService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISuggestedStopsService, SuggestedStopsService>();

builder.Services.AddScoped<IContext, ProjectTripsDataBase>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EnumService>();

builder.Services.AddAutoMapper(typeof(IMapper));
builder.Services.AddAutoMapper(typeof(MappingProfile));
var app = builder.Build();

// ------------------ בדיקת טבלאות EF ------------------
//using (var context = new ProjectTripsDataBase())
//{
//    var entityTypes = context.Model.GetEntityTypes();
//    foreach (var entityType in entityTypes)
//    {
//        var tableName = entityType.GetTableName();
//        var schema = entityType.GetSchema();
//        Console.WriteLine($"Entity: {entityType.Name}, Table: {tableName}, Schema: {schema}");
//    }
//}
// --------------------------------------------------------

//app.UseCors("AllowReact");
app.UseCors("AllowVitePorts");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

//להצפנת הסיסמאות השמורות כבר בDB
//using (var scope = app.Services.CreateScope())
//{
//    var context = scope.ServiceProvider.GetRequiredService<IContext>();

//    var users = await context.Users.ToListAsync();

//    foreach (var user in users)
//    {
//        if (!user.Password.StartsWith("$2")) // אם עדיין לא מוצפן
//        {
//            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
//        }
//    }

//    await context.save();
//}

app.Run();
