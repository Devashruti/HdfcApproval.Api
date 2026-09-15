using System.Text.Json;

namespace HdfcApproval.Api.DTOs.Requests
{
    public class UpdateRequestDto
    {
        public JsonElement BusinessData { get; set; } = new();
    }
}
