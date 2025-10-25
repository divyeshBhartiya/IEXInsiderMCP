using IEXInsiderMCP.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for browser access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register application services
builder.Services.AddSingleton<IEXDataService>();
builder.Services.AddScoped<NLPQueryService>();
builder.Services.AddScoped<MCPServer>(); // Unified MCP Server
builder.Services.AddScoped<MultiTimeSlotAnalyzer>(); // Multi-time-slot analyzer
builder.Services.AddScoped<AdvancedAnalyticsService>(); // Advanced analytics

// Register AI/ML services
builder.Services.AddScoped<InsightsEngine>();
builder.Services.AddScoped<NaturalLanguageEngine>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

// Serve static files (HTML frontend)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

// Load IEX data on startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting IEX Insider MCP Server...");

var dataService = app.Services.GetRequiredService<IEXDataService>();
logger.LogInformation("Loading IEX market data from Excel...");

try
{
    await dataService.LoadDataAsync();
    logger.LogInformation($"Successfully loaded {dataService.GetRecordCount()} records");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to load IEX data. Please ensure the Excel file path is correct in appsettings.json");
}

logger.LogInformation("IEX Insider MCP Server is running!");
logger.LogInformation("Access the web interface at: http://localhost:5000 or https://localhost:5001");
logger.LogInformation("API documentation available at: /swagger");

app.Run();
