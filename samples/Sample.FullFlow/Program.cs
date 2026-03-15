using Yarp.AiGateway.Core.Extensions;
using Yarp.ReverseProxy.Transforms;

// ═══════════════════════════════════════════════════════════════════
//  Sample.FullFlow — YARP as the single entry point
//
//  This sample demonstrates YARP routing to DIFFERENT backends:
//
//    /api/weather/*             → Internal Weather microservice (port 5100)
//    /api/catalog/*             → Internal Catalog microservice (port 5200)
//    /ai/chat/completions       → Azure OpenAI ☁️ (with AI Gateway guardrails)
//    /ai/local/chat/completions → Ollama LOCAL 🏠 (PHI data NEVER leaves your infra)
//
//  The AI Gateway guardrails are applied to ALL AI routes.
//  Regular microservice routes pass through YARP without guardrails.
//
//  Architecture:
//
//    Client
//      │
//      ▼
//    ┌────────────────────────────────────────────────────────────────┐
//    │  YARP Reverse Proxy (this app, port 5050)                     │
//    │                                                               │
//    │  /api/weather/*       ──► Weather µService (port 5100)        │
//    │  /api/catalog/*       ──► Catalog µService (port 5200)        │
//    │  /ai/chat/*           ──► [Guardrails] ──► Azure OpenAI ☁️    │
//    │  /ai/local/chat/*     ──► [Guardrails] ──► Ollama LOCAL 🏠    │
//    └────────────────────────────────────────────────────────────────┘
//
//  ⚠️  PHI (Protected Health Information) Use-Case:
//      When dealing with medical records, patient data, or any PHI,
//      data must NEVER leave the organization's infrastructure.
//      Route /ai/local/* sends data to a LOCAL Ollama instance —
//      same guardrails, same OpenAI-compatible API, ZERO data
//      exfiltration risk. No cloud provider ever sees the PHI.
//
// ═══════════════════════════════════════════════════════════════════

// ─── 1. Build the three apps ───────────────────────────────────────

var weatherApp = BuildWeatherMicroservice(args);
var catalogApp = BuildCatalogMicroservice(args);
var gatewayApp = BuildGateway(args);

// ─── 2. Start everything ───────────────────────────────────────────

await Task.WhenAll(
    weatherApp.RunAsync(),
    catalogApp.RunAsync(),
    gatewayApp.RunAsync()
);

// ═══════════════════════════════════════════════════════════════════
//  Weather Microservice (port 5100)
// ═══════════════════════════════════════════════════════════════════

static WebApplication BuildWeatherMicroservice(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls("http://localhost:5100");

    var app = builder.Build();

    var forecasts = new[]
    {
        new { City = "Madrid",    TempC = 28, Condition = "Sunny" },
        new { City = "London",    TempC = 14, Condition = "Cloudy" },
        new { City = "New York",  TempC = 22, Condition = "Partly cloudy" },
        new { City = "Tokyo",     TempC = 30, Condition = "Humid" },
        new { City = "Sydney",    TempC = 18, Condition = "Rainy" },
    };

    // GET /weather → list all forecasts
    app.MapGet("/weather", () =>
    {
        app.Logger.LogInformation("[Weather µService] GET /weather — returning {Count} forecasts", forecasts.Length);
        return Results.Ok(new { service = "weather-microservice", data = forecasts });
    });

    // GET /weather/{city} → single city forecast
    app.MapGet("/weather/{city}", (string city) =>
    {
        var match = forecasts.FirstOrDefault(f =>
            f.City.Equals(city, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            app.Logger.LogWarning("[Weather µService] GET /weather/{City} — not found", city);
            return Results.NotFound(new { error = $"No forecast for '{city}'" });
        }

        app.Logger.LogInformation("[Weather µService] GET /weather/{City} — {TempC}°C {Condition}", city, match.TempC, match.Condition);
        return Results.Ok(new { service = "weather-microservice", data = match });
    });

    // GET /weather/health → health check
    app.MapGet("/weather/health", () =>
    {
        app.Logger.LogInformation("[Weather µService] Health check OK");
        return Results.Ok(new { status = "healthy", service = "weather-microservice", timestamp = DateTime.UtcNow });
    });

    return app;
}

// ═══════════════════════════════════════════════════════════════════
//  Catalog Microservice (port 5200)
// ═══════════════════════════════════════════════════════════════════

static WebApplication BuildCatalogMicroservice(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls("http://localhost:5200");

    var app = builder.Build();

    var products = new[]
    {
        new { Id = 1, Name = "Laptop Pro 16",      Price = 1299.99m, Category = "Electronics" },
        new { Id = 2, Name = "Wireless Mouse",      Price = 29.99m,   Category = "Electronics" },
        new { Id = 3, Name = "Standing Desk",       Price = 449.00m,  Category = "Furniture" },
        new { Id = 4, Name = "Mechanical Keyboard",  Price = 89.99m,   Category = "Electronics" },
        new { Id = 5, Name = "Monitor 4K 27\"",     Price = 399.99m,  Category = "Electronics" },
    };

    // GET /catalog → list all products
    app.MapGet("/catalog", () =>
    {
        app.Logger.LogInformation("[Catalog µService] GET /catalog — returning {Count} products", products.Length);
        return Results.Ok(new { service = "catalog-microservice", data = products });
    });

    // GET /catalog/{id} → single product
    app.MapGet("/catalog/{id:int}", (int id) =>
    {
        var match = products.FirstOrDefault(p => p.Id == id);

        if (match is null)
        {
            app.Logger.LogWarning("[Catalog µService] GET /catalog/{Id} — not found", id);
            return Results.NotFound(new { error = $"Product {id} not found" });
        }

        app.Logger.LogInformation("[Catalog µService] GET /catalog/{Id} — {Name} ({Price:C})", id, match.Name, match.Price);
        return Results.Ok(new { service = "catalog-microservice", data = match });
    });

    // GET /catalog/search?q=... → search products by name
    app.MapGet("/catalog/search", (string? q) =>
    {
        var results = string.IsNullOrEmpty(q)
            ? products
            : products.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();

        app.Logger.LogInformation("[Catalog µService] GET /catalog/search?q={Query} — {Count} results", q, results.Length);
        return Results.Ok(new { service = "catalog-microservice", query = q, data = results });
    });

    // GET /catalog/health → health check
    app.MapGet("/catalog/health", () =>
    {
        app.Logger.LogInformation("[Catalog µService] Health check OK");
        return Results.Ok(new { status = "healthy", service = "catalog-microservice", timestamp = DateTime.UtcNow });
    });

    return app;
}

// ═══════════════════════════════════════════════════════════════════
//  YARP API Gateway (port 5050) — the single entry point
// ═══════════════════════════════════════════════════════════════════

static WebApplication BuildGateway(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseUrls("http://localhost:5050");

    // ─── YARP reverse proxy ────────────────────────────────────────
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms(context =>
        {
            // Only inject api-key for clusters that route to Azure OpenAI
            if (context.Route.ClusterId is "azure-openai" or "ollama-local")
            {
                // NOTE: ollama-local points to Azure OpenAI for TESTING only.
                // In production, Ollama runs locally with no auth needed.
                context.AddRequestTransform(tc =>
                {
                    var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? "";
                    tc.ProxyRequest.Headers.TryAddWithoutValidation("api-key", apiKey);
                    return ValueTask.CompletedTask;
                });
            }
        });

    // ─── AI Gateway guardrails ─────────────────────────────────────
    builder.Services.AddAiGatewayFromJson("aigateway.json");

    var app = builder.Build();

    // ─── Logging middleware to visualize the flow ───────────────────
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Gateway");

        logger.LogInformation(
            "══► Incoming: {Method} {Path}",
            context.Request.Method, context.Request.Path);

        await next();

        logger.LogInformation(
            "◄══ Response: {Method} {Path} → {StatusCode}",
            context.Request.Method, context.Request.Path, context.Response.StatusCode);
    });

    // ─── YARP pipeline ─────────────────────────────────────────────
    // AI Gateway guardrails run INSIDE the proxy pipeline.
    // Routes to microservices (/api/weather, /api/catalog) just pass through.
    // Routes to AI (/ai/chat/completions) go through the guardrails first.
    //
    // The guardrails detect prompt content only in POST/PUT/PATCH bodies
    // with OpenAI chat format — GET requests to microservices are untouched.
    app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UseAiGatewayGuardrails();
    });

    return app;
}
