using HdfcApproval.Api.Temporal.Models;
using HdfcApproval.Api.Temporal.Workflows;
using Temporalio.Client;

namespace HdfcApproval.Api.Services;

public class TemporalService
{
    private readonly ITemporalClient _client;

    private const string TaskQueue =
        "hdfc-approval-task-queue";

    public TemporalService(ITemporalClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Starts the approval workflow for a submitted request.
    /// </summary>
    public async Task<string> StartApprovalWorkflowAsync(
        ApprovalWorkflowInput input)
    {
        var workflowId =
            $"approval:{input.TenantId}:{input.RequestId}:r{input.Revision}";

        await _client.StartWorkflowAsync(
            (ApprovalWorkflow workflow) =>
                workflow.RunAsync(input),
            new WorkflowOptions
            {
                Id = workflowId,
                TaskQueue = TaskQueue
            });

        return workflowId;
    }

    /// <summary>
    /// Sends a human approval/rejection decision
    /// to the running Temporal workflow.
    /// </summary>
    public async Task SendDecisionAsync(
        string workflowId,
        string taskId,
        string decision)
    {
        var workflowHandle =
            _client.GetWorkflowHandle(workflowId);

        await workflowHandle.SignalAsync(
            (ApprovalWorkflow workflow) =>
                workflow.SubmitDecisionAsync(
                    taskId,
                    decision));
    }
}