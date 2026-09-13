using Freegram.Data;
using Freegram.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// CONTROLLERS
// ==========================================

builder.Services.AddControllers();

// ==========================================
// POSTGRESQL / ENTITY FRAMEWORK
// ==========================================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");
}

builder.Services.AddDbContext<FreegramDbContext>(options =>
    options.UseNpgsql(connectionString));

// ==========================================
// CORS
// ==========================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://freegram-frontend.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ==========================================
// SIGNALR
// ==========================================

builder.Services.AddSignalR();

// ==========================================
// JWT AUTHENTICATION
// ==========================================

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!
                        )
                    )
            };

        // JWT через SignalR WebSocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var path =
                    context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// SWAGGER
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

// ==========================================
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// SWAGGER
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ==========================================
// HTTPS
// ==========================================

// Render працює через HTTPS reverse proxy.
// ASP.NET Core всередині контейнера слухає HTTP.

// app.UseHttpsRedirection();

// ==========================================
// CORS
// ==========================================

app.UseCors("Frontend");

// ==========================================
// AUTHENTICATION / AUTHORIZATION
// ==========================================

app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// CONTROLLERS
// ==========================================

app.MapControllers();

// ==========================================
// SIGNALR
// ==========================================

app.MapHub<ChatHub>("/hubs/chat");

// ==========================================
// HEALTH CHECK / ROOT
// ==========================================

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    service = "Freegram Backend"
}));

// ==========================================
// RUN
// ==========================================

app.Run();