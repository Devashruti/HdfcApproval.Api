using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HdfcApproval.Api.Controllers
{
    [ApiController]
    [Route("v1")]
    [Authorize]
    public class MetadataController : ControllerBase
    {
        // GET /v1/modules
        [HttpGet("modules")]
        public IActionResult GetModules()
        {
            // Returns enabled modules permitted to the caller
            return Ok(new[]
            {
                new { moduleId = "LOAN", name = "Loan Management", enabled = true, activeTemplateId = "LOAN_STANDARD", activeVersion = 1 },
                new { moduleId = "EMPLOYEE", name = "Employee Management", enabled = true, activeTemplateId = "EMPLOYEE_STANDARD", activeVersion = 1 }
            });
        }

        // GET /v1/modules/{moduleId}/definition
        [HttpGet("modules/{moduleId}/definition")]
        public IActionResult GetModuleDefinition(string moduleId)
        {
            // Returns active template reference, form schema, and allowed views
            // Must use ETag in production implementation
            return Ok(new { moduleId = moduleId, schemaRef = $"{moduleId.ToLower()}-form-v1" });
        }

        // POST /v1/templates
        [HttpPost("templates")]
        [Authorize(Roles = "Admin")] // Requires administrative authorization
        public IActionResult CreateTemplateDraft([FromBody] object templateRequest)
        {
            // Creates a draft with templateId and moduleId
            return Created("/v1/templates/NEW_ID/versions/1", new { templateId = "NEW_ID", version = 1, status = "Draft" });
        }

        // PUT /v1/templates/{id}/versions/{v}
        [HttpPut("templates/{id}/versions/{v}")]
        [Authorize(Roles = "Admin")]
        public IActionResult EditTemplateDraft(
            string id,
            int v,
            [FromHeader(Name = "If-Match")] string eTag,
            [FromBody] object templatePayload)
        {
            if (string.IsNullOrWhiteSpace(eTag)) return BadRequest("If-Match header required for optimistic concurrency.");

            // Edit a draft only; reject if published
            return Ok(new { status = "Updated" });
        }

        // POST /v1/templates/{id}/versions/{v}/validate
        [HttpPost("templates/{id}/versions/{v}/validate")]
        [Authorize(Roles = "Admin")]
        public IActionResult ValidateTemplate(string id, int v)
        {
            // Validates stage IDs, valid roles, registered activities, and pinned schema
            // Returns field-level validation errors or a validated hash
            return Ok(new { validatedHash = "hash12345" });
        }

        // POST /v1/templates/{id}/versions/{v}/publish
        [HttpPost("templates/{id}/versions/{v}/publish")]
        [Authorize(Roles = "Admin")]
        public IActionResult PublishTemplate(string id, int v, [FromBody] object publishRequest)
        {
            // Publishes exactly the validated hash atomically
            return Ok(new { status = "Published", contentHash = "hash12345" });
        }

        // GET /v1/templates/{id}/versions/{v}
        [HttpGet("templates/{id}/versions/{v}")]
        [AllowAnonymous] // Or internal service authentication
        public IActionResult GetTemplateVersion(string id, int v)
        {
            // Returns immutable published configuration with contentHash
            return Ok(new
            {
                templateId = id,
                version = v,
                status = "Published",
                contentHash = "hash12345",
                stages = new[]
                {
                    new { stageId = "checker", role = "CHECKER", order = 1 },
                    new { stageId = "validator", role = "VALIDATOR", order = 2 }
                }
            });
        }
    }
}