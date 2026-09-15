using System.Collections.Generic;
using System.Threading.Tasks;
using HdfcApproval.Api.Models; // Assumes this is where DecisionCommand lives
using HdfcApproval.Api.Temporal.Activities;
using HdfcApproval.Api.Temporal.Models;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using System;

namespace HdfcApproval.Api.Temporal.Workflows
{
    [Workflow]
    public class ApprovalWorkflow
    {
        private bool _ready = false;
        private bool _finishing = false;
        private string _state = "SUBMITTING";

        // Tracks completed commands to deduplicate concurrent approval attempts
        private readonly Dictionary<string, ApprovalWorkflowResult> _completedCommands = new();

        [WorkflowRun]
        public async Task<ApprovalWorkflowResult> RunAsync(ApprovalWorkflowInput input)
        {
            // 1. Initialize workflow and create the first task (atomically)
            var initialState = await Workflow.ExecuteActivityAsync(
                (ApprovalActivities activities) => activities.InitializeApprovalV1(input),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

            _state = initialState.State;
            _ready = true;

            // 2. Suspend workflow execution until the state is no longer pending
            await Workflow.WaitConditionAsync(() =>
                _state == "APPROVED" || _state == "REJECTED");

            _finishing = true;

            // 3. (Phase 4) Execute PDF/Processing completion activities here if Approved

            return new ApprovalWorkflowResult { State = _state };
        }

        // Changed from Signal to Update to return a synchronous result to the API
        [WorkflowUpdate]
        public async Task<ApprovalWorkflowResult> SubmitDecision(DecisionCommand command)
        {
            // Wait until workflow initialization is complete
            await Workflow.WaitConditionAsync(() => _ready);

            // Deduplicate: If this command ID already ran, return the stored result
            if (_completedCommands.TryGetValue(command.CommandId, out var storedResult))
            {
                return storedResult;
            }

            if (_finishing)
            {
                throw new ApplicationFailureException("Workflow is already finishing.");
            }

            // Atomically check access, close task, create next task, and write outbox
            var result = await Workflow.ExecuteActivityAsync(
                (ApprovalActivities activities) => activities.CommitDecisionAndRouteV1(command),
                new ActivityOptions
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    // ScheduleToClose, RetryPolicies, etc., go here
                });

            if (result.Applied)
            {
                _state = result.State;
                _completedCommands[command.CommandId] = result;
            }

            return result;
        }

        // Validators must be synchronous and side-effect free
        [WorkflowUpdateValidator(nameof(SubmitDecision))]
        public void ValidateSubmitDecision(DecisionCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.CommandId))
            {
                throw new ApplicationFailureException("CommandId is required.");
            }
        }
    }
}