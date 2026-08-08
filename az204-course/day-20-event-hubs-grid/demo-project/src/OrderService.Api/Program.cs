using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Event Grid
builder.Services.Configure<EventGridOptions>(
    builder.Configuration.GetSection("EventGrid"));

builder.Services.AddSingleton<IEventGridPublisher, EventGridPublisher>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public class EventGridOptions
{
    public string TopicEndpoint { get; set; } = string.Empty;
    public string TopicKey { get; set; } = string.Empty;
}
