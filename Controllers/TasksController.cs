using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HdfcApproval.Api.Data;
using HdfcApproval.Api.Models;

namespace HdfcApproval.Api.Controllers
{
    [ApiController]
    [Route("v1/tasks")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly ApprovalDbContext _db;

        public TasksController(ApprovalDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] string moduleId,
            [FromQuery] string role,
            [FromQuery] string status = "PENDING",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // The backend enforces eligibility; it never trusts a browser-supplied role.
            var actorId = User.Identity?.Name ?? "UNKNOWN";

            // Example access check: 
             if (!User.IsInRole($"{moduleId}_{role}")) return Forbid();

            // The query is indexed by module, role, status, and opened_at.
            var query = _db.HumanTasks
                .Where(t => t.Status == status && t.StageId == role)
                .Join(
                    _db.BusinessRequests,
                    task => task.RequestId,
                    req => req.RequestId,
                    (task, req) => new { Task = task, Request = req }
                )
                .Where(x => x.Request.ModuleId == moduleId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(x => x.Task.OpenedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    TaskId = x.Task.TaskId,
                    RequestId = x.Task.RequestId,
                    PreviousStage = x.Request.ApprovalStatus,
                    OpenedAt = x.Task.OpenedAt,
                    AgeInHours = (DateTime.UtcNow - x.Task.OpenedAt)*24
                })
                .ToListAsync();

            return Ok(new
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpPost("{taskId}/decisions")]
        public async Task<IActionResult> SubmitDecision(
            string taskId,
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] DecisionCommand command)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest("Idempotency-Key is required.");
            }

            command.ActorId = User.Identity?.Name ?? "UNKNOWN";
            command.TaskId = taskId;
            command.CommandId = idempotencyKey;

            // Write the durable command to the outbox for the dispatcher
            var outboxEvent = new OutboxEvent
            {
                Type = "SUBMIT_DECISION",
                AggregateId = $"approval:wf:{command.CommandId}",
                PayloadRef = JsonSerializer.Serialize(command),
                DeliveryState = "PENDING",
                NextAttemptAt = DateTime.UtcNow
            };

            _db.OutboxEvents.Add(outboxEvent);
            await _db.SaveChangesAsync();

            // Return 202 while the Temporal workflow processes the update
            return Accepted($"/v1/commands/{command.CommandId}", new
            {
                commandId = command.CommandId,
                status = "PENDING"
            });
        }
    }
}