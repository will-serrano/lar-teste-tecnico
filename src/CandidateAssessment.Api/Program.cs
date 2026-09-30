using CandidateAssessment.Api;
using CandidateAssessment.Api.Middleware;
using CandidateAssessment.Application;
using CandidateAssessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Services are registered via per-layer extension methods.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory<Program> in tests.
public partial class Program
{
}
