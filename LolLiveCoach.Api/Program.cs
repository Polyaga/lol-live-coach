using LolLiveCoach.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<FreemiumOptions>(builder.Configuration.GetSection(FreemiumOptions.SectionName));
builder.Services.Configure<PlatformOptions>(builder.Configuration.GetSection(PlatformOptions.SectionName));
builder.Services.Configure<RiotApiOptions>(builder.Configuration.GetSection(RiotApiOptions.SectionName));

builder.Services.AddHttpClient<LiveGameService>(client =>
{
    client.BaseAddress = new Uri("https://127.0.0.1:2999");
    client.Timeout = TimeSpan.FromSeconds(2);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

builder.Services.AddScoped<AdviceService>();
builder.Services.AddScoped<EnemyTeamAnalyzer>();
builder.Services.AddScoped<BuildRecommendationService>();
builder.Services.AddScoped<PlayerProfileService>();
builder.Services.AddScoped<RoleDetectorService>();
builder.Services.AddScoped<SubscriptionAccessService>();
builder.Services.AddHttpClient<RemotePlatformAccessService>();
builder.Services.AddHttpClient<RemotePlayerProfileService>();
builder.Services.AddHttpClient<RiotItemCatalogService>(client =>
{
    client.BaseAddress = new Uri("https://ddragon.leagueoflegends.com/");
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHttpClient<RiotPlayerProfileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
