using System;
using System.Threading.Tasks;
using HdfcApproval.Api.Data;
using HdfcApproval.Api.Models;
using HdfcApproval.Api.Temporal.Models;
using Microsoft.EntityFrameworkCore;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace HdfcApproval.Api.Temporal.Activities
{
    public class ApprovalActivities
    {
        private readonly ApprovalDbContext _db;

        public ApprovalActivities(ApprovalDbContext db)
        {
            _db = db;
        }

        [Activity]
        public async Task<ApprovalWorkflowResult> InitializeApprovalV1(ApprovalWorkflowInput input)
        {
            var request = await _db.BusinessRequests.FirstOrDefaultAsync(x => x.RequestId == input.RequestId);
            if (request == null) throw new ApplicationFailureException($"Request {input.RequestId} not found.");

            var firstStageId = "CHECKER"; // In a full implementation, this comes from the Template Snapshot
            var taskId = $"T-{input.RequestId}-{firstStageId}";

            // Idempotency: If the task already exists, just return the state
            var existingTask = await _db.HumanTasks.FirstOrDefaultAsync(x => x.TaskId == taskId);
            if (existingTask != null)
            {
                return new ApprovalWorkflowResult { State = request.ApprovalStatus };
            }

            // 1. Create the initial task
            var task = new Humantask
            {
                TaskId = taskId,
                RequestId = input.RequestId,
                StageId = firstStageId,
                StageInstanceId = $"{input.RequestId}-{firstStageId}-1",
                Status = "PENDING",
                Version = 1,
                OpenedAt = DateTime.UtcNow
            };
            _db.HumanTasks.Add(task);

            // 2. Update the business request status atomically
            request.ApprovalStatus = "PENDING_CHECKER";

            await _db.SaveChangesAsync();

            return new ApprovalWorkflowResult { State = request.ApprovalStatus };
        }

        [Activity]
        public async Task<ApprovalWorkflowResult> CommitDecisionAndRouteV1(DecisionCommand command)
        {
            var request = await _db.BusinessRequests.FirstOrDefaultAsync(x => x.RequestId == command.CommandId);
            var currentTask = await _db.HumanTasks.FirstOrDefaultAsync(x => x.TaskId == command.TaskId);

            if (request == null || currentTask == null)
                throw new ApplicationFailureException("Record or Task not found.");

            // Idempotency: Return stored result if this command was already processed
            if (currentTask.Status != "PENDING")
            {
                return new ApprovalWorkflowResult { State = request.ApprovalStatus, Applied = true };
            }

            // Separation of Duties Verification
            if (request.MakerId == command.ActorId)
            {
                throw new ApplicationFailureException("A Maker cannot approve their own submission.");
            }

            var priorApproval = await _db.HumanTasks.AnyAsync(x =>
                x.RequestId == request.RequestId &&
                x.DecidedBy == command.ActorId &&
                x.Status == "APPROVED");

            if (priorApproval)
            {
                throw new ApplicationFailureException("Actor has already approved a prior stage for this request.");
            }

            // Apply Decision
            currentTask.Status = command.Decision == "APPROVE" ? "APPROVED" : "REJECTED";
            currentTask.DecidedBy = command.ActorId;
            currentTask.DecidedAt = DateTime.UtcNow;
            currentTask.Version++;

            if (command.Decision == "REJECT")
            {
                request.ApprovalStatus = "REJECTED";
            }
            else
            {
                // Route to next stage based on current task
                // In Phase 2, this logic should read from the immutable template snapshot rather than hardcoding.
                if (currentTask.StageId == "CHECKER")
                {
                    request.ApprovalStatus = "PENDING_VALIDATOR";
                    _db.HumanTasks.Add(CreateNextTask(request.RequestId, "VALIDATOR", 2));
                }
                else if (currentTask.StageId == "VALIDATOR" && request.ModuleId == "LOAN")
                {
                    request.ApprovalStatus = "PENDING_REVIEWER";
                    _db.HumanTasks.Add(CreateNextTask(request.RequestId, "REVIEWER", 3));
                }
                else
                {
                    // Terminal approval reached
                    request.ApprovalStatus = "APPROVED";
                }
            }

            // Write audit event
            _db.AuditEvents.Add(new AuditEvent
            {
                RequestId = request.RequestId,
                ActorId = command.ActorId,
                Action = command.Decision,
                FromState = currentTask.Status,
                ToState = request.ApprovalStatus,
                Timestamp = DateTime.UtcNow,
                CommandId = command.CommandId
            });

            // Single transaction commit for task, routing, status, and audit
            await _db.SaveChangesAsync();

            return new ApprovalWorkflowResult { State = request.ApprovalStatus, Applied = true };
        }

        private Humantask CreateNextTask(string requestId, string role, int level)
        {
            return new Humantask
            {
                TaskId = $"T-{requestId}-{role}",
                RequestId = requestId,
                StageId = role,
                StageInstanceId = $"{requestId}-{role}-{level}",
                Status = "PENDING",
                Version = 1,
                OpenedAt = DateTime.UtcNow
            };
        }
    }
}