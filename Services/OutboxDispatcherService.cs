using HdfcApproval.Api.Data;
using HdfcApproval.Api.Temporal.Models;
using HdfcApproval.Api.Temporal.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;

namespace HdfcApproval.Api.Services
{
    public class OutboxDispatcherService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITemporalClient _temporalClient;
        private readonly ILogger<OutboxDispatcherService> _logger;

        public OutboxDispatcherService(
            IServiceProvider serviceProvider,
            ITemporalClient temporalClient,
            ILogger<OutboxDispatcherService> logger)
        {
            _serviceProvider = serviceProvider;
            _temporalClient = temporalClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOutboxEventsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcessOutboxEventsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApprovalDbContext>();

            var pendingEvents = await dbContext.OutboxEvents
                .Where(e => e.DeliveryState == "PENDING" && e.NextAttemptAt <= DateTime.UtcNow)
                .OrderBy(e => e.EventId)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var evt in pendingEvents)
            {
                try
                {
                    if (evt.Type == "START_WORKFLOW")
                    {
                        var workflowInput = JsonSerializer.Deserialize<ApprovalWorkflowInput>(evt.PayloadRef);
                        // Step 3: Dispatcher starts GenericApprovalWorkflow using a deterministic workflowId
                        //await _temporalClient.StartWorkflowAsync(
                        //    (ApprovalWorkflow wf) => wf.RunAsync(evt.PayloadRef),
                        //    new WorkflowOptions(id: evt.AggregateId, taskQueue: "approval-workflows")
                        //);
                        await _temporalClient.StartWorkflowAsync(
                    (ApprovalWorkflow wf) => wf.RunAsync(workflowInput),
                    new WorkflowOptions(id: evt.AggregateId, taskQueue: "approval-workflows")
                );
                    }
                    else if (evt.Type == "SUBMIT_DECISION")
                    {
                        var commandIndput = JsonSerializer.Deserialize<ApprovalWorkflowInput>(evt.PayloadRef);
                        // Step 5: Dispatcher sends a Temporal Update using updateId=commandId
                        //var handle = _temporalClient.GetWorkflowHandle(evt.AggregateId);
                        //await handle.ExecuteUpdateAsync(
                        //    wf => wf.SubmitDecision(evt.PayloadRef),
                        //    new WorkflowUpdateOptions { Id = evt.EventId } // Deduplicates by command ID
                        //);
                        var handle = _temporalClient.GetWorkflowHandle(evt.AggregateId);
                        await handle.ExecuteUpdateAsync("SubmitDecision",                              // update name registered by the workflow
                                        [commandIndput],
                            new WorkflowUpdateOptions { Id = commandIndput.RequestId }
                        );
                    }

                    evt.DeliveryState = "DELIVERED";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch outbox event {EventId}", evt.EventId);
                    evt.AttemptCount++;
                    // Exponential backoff for retry budgets
                    evt.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, evt.AttemptCount));
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}