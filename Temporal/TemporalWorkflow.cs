using HdfcApproval.Api.Temporal.Activities;
using HdfcApproval.Api.Temporal.Workflows;
using Temporalio.Client;
using Temporalio.Worker;

namespace HdfcApproval.Api.Temporal
{
    public class TemporalWorkflow : BackgroundService
    {
        private readonly TemporalClient _temporalClient;
        private readonly ApprovalActivities _approvalActivities;
        private readonly ILogger<TemporalWorkflow> _logger;

        private const string TaskQueue = "hdfc-approval-task-queue";
        public TemporalWorkflow(TemporalClient temporalClient, ApprovalActivities approvalActivities, ILogger<TemporalWorkflow> logger)
        {
            _temporalClient = temporalClient;
            _approvalActivities = approvalActivities;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var worker = new Temporalio.Worker.TemporalWorker(_temporalClient, 
                new TemporalWorkerOptions(TaskQueue)
                .AddWorkflow<ApprovalWorkflow>()
                .AddActivity(_approvalActivities.CreateApprovalTaskAsync)
                .AddActivity(_approvalActivities.UpdateApprovalStatusAsync)
                .AddActivity(_approvalActivities.CompleteTaskAsync));
            
            await worker.ExecuteAsync(stoppingToken);
        }
    }
}
