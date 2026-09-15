using HdfcApproval.Api.Data;
using HdfcApproval.Api.DTOs.Requests;
using HdfcApproval.Api.Models;
using HdfcApproval.Api.Services;
using HdfcApproval.Api.Temporal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
    

namespace HdfcApproval.Api.Controllers;

[ApiController]
[Route("v1/requests")]
public class RequestsController : ControllerBase
{
    private readonly ApprovalDbContext _db;
    private readonly TemporalService _temporalService;

    public RequestsController(ApprovalDbContext db, TemporalService temporalService)
    {
        _db = db;
    }

    private async Task<string> GenerateRequestIdAsync(string moduleId)
    {
        var prefix = moduleId.Equals(
            "LOAN",
            StringComparison.OrdinalIgnoreCase)
            ? "LN"
            : "EMP";

        var count = await _db.BusinessRequests
            .CountAsync(x => x.ModuleId == moduleId);

        return $"{prefix}-{count + 1:D3}";
    }
    // ============================================================
    // POST /v1/requests
    // Create a new DRAFT request
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ModuleId))
        {
            return BadRequest(new
            {
                message = "moduleId is required."
            });
        }

        if (dto.BusinessData.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new
            {
                message = "businessData must be an object."
            });
        }

        if (!dto.ModuleId.Equals("LOAN",
            StringComparison.OrdinalIgnoreCase) &&
            !dto.ModuleId.Equals("EMPLOYEE",
            StringComparison.OrdinalIgnoreCase))
        {
            return UnprocessableEntity(new
            {
                message = $"Unsupported moduleId: {dto.ModuleId}"
            });
        }


        var requestId = await GenerateRequestIdAsync(dto.ModuleId);

        var request = new BusinessRequest
        {
            RequestId = requestId,

            // These will eventually come from authenticated user/tenant
            // rather than the request body.
            TenantId = "DEFAULT_TENANT",
            MakerId = "CURRENT_USER",

            ModuleId = dto.ModuleId.ToUpperInvariant(),

            Revision = 1,

            ApprovalStatus = "DRAFT",
            ProcessingStatus = "NOT_STARTED",
            StateVersion = "1",

            TemplateId = dto.ModuleId.ToUpperInvariant(),
            TemplateVersion = "1",
            SnapshotHash = null,

            // Temporal will populate this later.
            WorkflowId = string.Empty
        };

        _db.BusinessRequests.Add(request);

        // --------------------------------------------------------
        // Save module-specific business data
        // --------------------------------------------------------

        if (dto.ModuleId.Equals(
                "LOAN",
                StringComparison.OrdinalIgnoreCase))
        {
            var loanData = JsonSerializer.Deserialize<Loandata>(
                dto.BusinessData.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (loanData == null)
            {
                return BadRequest(new
                {
                    message = "Invalid loan businessData."
                });
            }

            loanData.RequestId = requestId;

            _db.LoansData.Add(loanData);
        }
        else if (dto.ModuleId.Equals(
                     "EMPLOYEE",
                     StringComparison.OrdinalIgnoreCase))
        {
            var employeeData = JsonSerializer.Deserialize<Employeedata>(
                dto.BusinessData.GetRawText());

            if (employeeData == null)
            {
                return BadRequest(new
                {
                    message = "Invalid employee businessData."
                });
            }

            employeeData.RequestId = requestId;

            _db.EmployeesData.Add(employeeData);
        }
        else
        {
            return BadRequest(new
            {
                message = $"Unsupported moduleId: {dto.ModuleId}"
            });
        }

        await _db.SaveChangesAsync();

        //var response = new RequestResponseDto
        //{
        //    RequestId = request.RequestId,
        //    Revision = request.Revision
        //};

        return CreatedAtAction(
            nameof(GetRequest),
            new { id = request.RequestId },
            new
            {
                requestId,
                revision = request.Revision
            });
    }



    // ============================================================
    // GET /v1/requests/{id}
    // Get request details
    // ============================================================

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRequest(string id)
    {
        var request = await _db.BusinessRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestId == id);

        if (request == null)
        {
            return NotFound(new
            {
                message = $"Request {id} not found."
            });
        }

        object? businessData = null;

        if (request.ModuleId.Equals(
                "LOAN",
                StringComparison.OrdinalIgnoreCase))
        {
            businessData = await _db.LoansData
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestId == id);
        }
        else if (request.ModuleId.Equals(
                     "EMPLOYEE",
                     StringComparison.OrdinalIgnoreCase))
        {
            businessData = await _db.EmployeesData
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestId == id);
        }

        return Ok(new
        {
            requestId = request.RequestId,
            moduleId = request.ModuleId,
            revision = request.Revision,
            approvalStatus = request.ApprovalStatus,
            processingStatus = request.ProcessingStatus,
            stateVersion = request.StateVersion,
            workflowId = request.WorkflowId,
            businessData
        });
    }


    // ============================================================
    // PATCH /v1/requests/{id}
    // Maker edits DRAFT only
    // If-Match revision required
    // ============================================================

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateRequest(
        string id,
        [FromHeader(Name = "If-Match-Revision")] int? ifMatch,
        [FromBody] UpdateRequestDto dto)
    {
        if (!ifMatch.HasValue)
        {
            return BadRequest(new
            {
                message = "If-Match header is required."
            });
        }

        if (dto.BusinessData.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new
            {
                message = "businessData must be an object."
            });
        }
        var request = await _db.BusinessRequests
            .FirstOrDefaultAsync(x => x.RequestId == id);

        if (request == null)
        {
            return NotFound(new
            {
                message = $"Request {id} not found."
            });
        }

        // --------------------------------------------------------
        // Only DRAFT can be edited
        // --------------------------------------------------------

        if (!request.ApprovalStatus.Equals(
                "DRAFT",
                StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                message = "Only DRAFT requests can be edited."
            });
        }

        // --------------------------------------------------------
        // Optimistic concurrency check
        // --------------------------------------------------------

        if (request.Revision != ifMatch.Value)
        {
            return Conflict(new
            {
                message = "Stale revision.",
                currentRevision = request.Revision
            });
        }

        // --------------------------------------------------------
        // Update module-specific business data
        // --------------------------------------------------------

        if (request.ModuleId.Equals(
                "LOAN",
                StringComparison.OrdinalIgnoreCase))
        {
            var loanData = await _db.LoansData
                .FirstOrDefaultAsync(x => x.RequestId == id);

            if (loanData == null)
            {
                return NotFound(new
                {
                    message = "Loan data not found."
                });
            }

            var updatedLoan =
                JsonSerializer.Deserialize<Loandata>(
                    dto.BusinessData.GetRawText(),new JsonSerializerOptions { PropertyNameCaseInsensitive=true});

            if (updatedLoan == null)
            {
                return BadRequest(new
                {
                    message = "Invalid loan businessData."
                });
            }

            loanData.CustomerId = updatedLoan.CustomerId;
            loanData.CustomerName = updatedLoan.CustomerName;
            loanData.LoanAmount = updatedLoan.LoanAmount;
            loanData.Currency = updatedLoan.Currency;
            loanData.EncryptedPan = updatedLoan.EncryptedPan;
            loanData.Dob = updatedLoan.Dob;
            loanData.Mobile = updatedLoan.Mobile;
        }
        else if (request.ModuleId.Equals(
                     "EMPLOYEE",
                     StringComparison.OrdinalIgnoreCase))
        {
            var employeeData = await _db.EmployeesData
                .FirstOrDefaultAsync(x => x.RequestId == id);

            if (employeeData == null)
            {
                return NotFound(new
                {
                    message = "Employee data not found."
                });
            }

            var updatedEmployee =
                JsonSerializer.Deserialize<Employeedata>(
                    dto.BusinessData.GetRawText());

            if (updatedEmployee == null)
            {
                return BadRequest(new
                {
                    message = "Invalid employee businessData."
                });
            }

            employeeData.EmployeeCode = updatedEmployee.EmployeeCode;
            employeeData.EmployeeName = updatedEmployee.EmployeeName;
            employeeData.DepartmentId = updatedEmployee.DepartmentId;
            employeeData.Salary = updatedEmployee.Salary;
            employeeData.Currency = updatedEmployee.Currency;
            employeeData.SalaryPeriod = updatedEmployee.SalaryPeriod;
            employeeData.JoiningDate = updatedEmployee.JoiningDate;
        }
        else
        {
            return BadRequest(new
            {
                message = $"Unsupported moduleId: {request.ModuleId}"
            });
        }

        // Increment revision after successful update
        request.Revision++;

        // State version can also represent the new aggregate version
        request.StateVersion = request.Revision.ToString();

        await _db.SaveChangesAsync();

        return Ok(new RequestResponseDto
        {
            RequestId = request.RequestId,
            Revision = request.Revision
        });
    }


    // ============================================================
    // POST /v1/requests/{id}/submit
    // Submit DRAFT for approval
    // ============================================================

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitRequest(
        string id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] SubmitRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new
            {
                message = "Idempotency-Key header is required."
            });
        }

        var request = await _db.BusinessRequests
            .FirstOrDefaultAsync(x => x.RequestId == id);

        if (request == null)
        {
            return NotFound(new
            {
                message = $"Request {id} not found."
            });
        }

        // --------------------------------------------------------
        // Request must still be DRAFT
        // --------------------------------------------------------

        if (!request.ApprovalStatus.Equals(
                "DRAFT",
                StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                message = "Only DRAFT requests can be submitted."
            });
        }

        // --------------------------------------------------------
        // Revision check
        // --------------------------------------------------------

        if (request.Revision != dto.ExpectedRevision)
        {
            return Conflict(new
            {
                message = "Expected revision does not match current revision.",
                currentRevision = request.Revision
            });
        }

        // --------------------------------------------------------
        // TEMPORAL workflow
        // --------------------------------------------------------

        var workflowInput = new ApprovalWorkflowInput
        {
            RequestId = request.RequestId,
            ModuleId = request.ModuleId,
            Revision = request.Revision,
            TenantId = request.TenantId
        };

        var workflowId =
            await _temporalService.StartApprovalWorkflowAsync(
                workflowInput);

        request.WorkflowId = workflowId;

        request.ApprovalStatus = "SUBMITTED";

        await _db.SaveChangesAsync();

        // Later this will point to:
        // GET /v1/commands/{commandId}

        return Accepted(new
        {
            requestId = request.RequestId,
            revision = request.Revision,
            status = "SUBMITTED"
        });
    }
}