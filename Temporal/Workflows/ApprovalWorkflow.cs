using HdfcApproval.Api.Temporal.Activities;
using HdfcApproval.Api.Temporal.Models;
using Temporalio.Workflows;

namespace HdfcApproval.Api.Temporal.Workflows
{
    [Workflow]
    public class ApprovalWorkflow
    {
        private readonly Dictionary<string, string> _decisions = new();
        [WorkflowRun]
        public async Task<string> RunAsync(ApprovalWorkflowInput input)
        {
            // 1. Mark request as SUBMITTING
            await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.UpdateApprovalStatusAsync(input.RequestId, "SUBMITTING"),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

            // 2. Create CHECKER task
            var checkerTaskId = $"T-{input.RequestId}-CHECKER";
            await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.CreateApprovalTaskAsync(input.RequestId, "CHECKER", 1),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

            // 3. Wait for CHECKER decision
            await Workflow.WaitConditionAsync(() => _decisions.ContainsKey(checkerTaskId));
            var checkerDecision = _decisions[checkerTaskId];
            if (checkerDecision == "REJECT")
            {
                await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.UpdateApprovalStatusAsync(input.RequestId, "REJECTED"),
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
                return "REJECTED";
            }

            // 4. Determine approval levels 
            var approvalLevels = input.ModuleId.Equals("LOAN", StringComparison.OrdinalIgnoreCase) ? 3 : 2;

            //5. Sequential Approval
            for (var level = 1; level <= approvalLevels; level++)
            {
                var role = $"APPROVER_{level}";
                var taskId = $"T-{input.RequestId}-{role}";
                await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.CreateApprovalTaskAsync(input.RequestId, role, level),
                    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
                // Wait indefinitely for human decision
                await Workflow.WaitConditionAsync(() => _decisions.ContainsKey(taskId));
                var decision = _decisions[taskId];
                if (decision == "REJECT")
                {
                    await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.UpdateApprovalStatusAsync(input.RequestId, "REJECTED"),
                        new ActivityOptions
                        {
                            StartToCloseTimeout = TimeSpan.FromMinutes(1)
                        });
                    return "REJECTED";
                }
            }
            //6. All approvals completed
            await Workflow.ExecuteActivityAsync((ApprovalActivities activities) => activities.UpdateApprovalStatusAsync(input.RequestId, "APPROVED"),
                new ActivityOptions
                {
                    StartToCloseTimeout = TimeSpan.FromMinutes(1)
                });
            return "APPROVED";

        }
        //Signal form TasksController
        [WorkflowSignal]
        public Task SubmitDecisionAsync(string taskId, string decision)
        {
            _decisions[taskId] = decision;
            return Task.CompletedTask;
        }
    }
}
