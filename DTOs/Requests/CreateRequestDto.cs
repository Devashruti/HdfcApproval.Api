using System.Text.Json;

namespace HdfcApproval.Api.DTOs.Requests
{
    public class CreateRequestDto
    {
        public string ModuleId { get; set; } = string.Empty;

        public JsonElement BusinessData { get; set; } = new();
    }
}
