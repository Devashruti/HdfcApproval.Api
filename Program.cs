using HdfcApproval.Api.Data;
using HdfcApproval.Api.Temporal;
using HdfcApproval.Api.Temporal.Activities;
using Microsoft.EntityFrameworkCore;
using Temporalio.Client;
using Temporalio.Worker;
using HdfcApproval.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApprovalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ApprovalDb")));
// register swagger generators
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//temporal client
var temporalClient =
    await TemporalClient.ConnectAsync(
        new TemporalClientConnectOptions
        {
            TargetHost = "localhost:7233",
            Namespace = "default"
        });

builder.Services.AddSingleton<ITemporalClient>(temporalClient);

//temporal services
builder.Services.AddScoped<ApprovalActivities>();

builder.Services.AddScoped<TemporalService>();

builder.Services.AddHostedService<TemporalWorkflow>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
