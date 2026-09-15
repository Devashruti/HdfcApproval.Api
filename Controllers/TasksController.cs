using Microsoft.AspNetCore.Mvc;

namespace HdfcApproval.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        //get: api/Metadata/loan
        [HttpGet("loan")]
        public IActionResult GetLoanMetaData()
        {
            var metadata = new
            {

            };
            return Ok(metadata);
        }
    }
}
