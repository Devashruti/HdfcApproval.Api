using HdfcApproval.Api.Data;
using HdfcApproval.Api.Models;
using Microsoft.EntityFrameworkCore;
using Temporalio.Activities;

namespace HdfcApproval.Api.Temporal.Activities
{
    public class ApprovalActivities
    {
        private readonly ApprovalDbContext _db;
        public ApprovalActivities(ApprovalDbContext db)
        { _db = db; }

        [Activity]
        public async Task CreateApprovalTaskAsync(string requestId, string role, int level)
        {
            var taskId = $"T-{requestId}-{role}";
            var existingTask = await _db.HumanTasks.FirstOrDefaultAsync(x => x.TaskId == taskId);
            if (existingTask != null)
            {
                return;
            }
            var task = new Humantask
            {
                TaskId = taskId,
                RequestId = requestId,
                StageId = role,
                StageInstanceId = $"{requestId}-{role}-{level}",
                Status = "PENDING",
                Version = 1,
                OpenedAt = DateTime.UtcNow
            };
            _db.HumanTasks.Add(task);
            await _db.SaveChangesAsync();
        }
        [Activity]
        public async Task UpdateApprovalStatusAsync(string requestId, string status)
        {
            var request = await _db.BusinessRequests.FirstOrDefaultAsync(x => x.RequestId == requestId);
            if (request == null)
            {
                throw new InvalidOperationException($"Request {requestId} not found.");
            }
            request.ApprovalStatus = status; await _db.SaveChangesAsync();
        }
        [Activity]
        public async Task CompleteTaskAsync(string taskId, string decision, string decidedBy)
        {
            var task = await _db.HumanTasks.FirstOrDefaultAsync(x => x.TaskId == taskId);
            if (task == null)
            {
                throw new InvalidOperationException($"Task {taskId} not found.");
            }
            task.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
            task.DecidedBy = decidedBy;
            task.DecidedAt = DateTime.UtcNow;
            task.Version++;
            await _db.SaveChangesAsync();
        }
        [Activity]
        public async Task AutoRejectTaskAsync(string taskId)
        {
            var task = await _db.HumanTasks.FirstOrDefaultAsync(x => x.TaskId == taskId);
            if (task == null)
            {
                throw new InvalidOperationException($"Task {taskId} not found.");
            }
            if (task.Status != "PENDING")
            {
                return;
            }

            task.Status = "REJECTED";
            task.DecidedBy = "SYSTEM";
            task.DecidedAt = DateTime.UtcNow;
            task.Version++;
            await _db.SaveChangesAsync();
        }
    }
}
