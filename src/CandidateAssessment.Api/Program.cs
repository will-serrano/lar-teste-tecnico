using CandidateAssessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Services will be registered via extension methods:
// builder.Services
//     .AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

// builder.Services
//     .AddPresentation(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
