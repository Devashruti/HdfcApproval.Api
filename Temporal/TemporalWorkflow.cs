using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using HdfcApproval.Api.Temporal.Activities;
using HdfcApproval.Api.Temporal.Workflows;

namespace HdfcApproval.Api.Temporal
{
    public class TemporalWorkerService : BackgroundService
    {
        private readonly ITemporalClient _temporalClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TemporalWorkerService> _logger;

        // Queue name updated to match the architecture specification and dispatcher
        private const string TaskQueue = "approval-workflows";

        public TemporalWorkerService(
            ITemporalClient temporalClient,
            IServiceProvider serviceProvider,
            ILogger<TemporalWorkerService> logger)
        {
            _temporalClient = temporalClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Temporal Worker on queue {Queue}", TaskQueue);

            using var scope = _serviceProvider.CreateScope();
            var activities = scope.ServiceProvider.GetRequiredService<ApprovalActivities>();

            var workerOptions = new TemporalWorkerOptions(TaskQueue)
                .AddWorkflow<ApprovalWorkflow>()
                // Register exactly the two consolidated, atomic activities required by the spec
                .AddActivity(activities.InitializeApprovalV1)
                .AddActivity(activities.CommitDecisionAndRouteV1);
            // Removed the granular UpdateApprovalStatusAsync and CompleteTaskAsync

            using var worker = new TemporalWorker(_temporalClient, workerOptions);

            try
            {
                await worker.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Temporal Worker execution cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Temporal Worker encountered a fatal error.");
            }
        }
    }
}