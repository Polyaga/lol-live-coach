using LolLiveCoach.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddScoped<RoleDetectorService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

app.Run();