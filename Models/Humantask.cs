namespace HdfcApproval.Api.Models
{
    public class Humantask
    {
        public string TaskId { get; set; }

        public string RequestId { get; set; }

        public string StageId { get; set; } = string.Empty;

        public string StageInstanceId { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = "PENDING";

        public long Version { get; set; } = 1;

        public DateTime? OpenedAt { get; set; }

        public string? DecidedBy { get; set; }

        public DateTime? DecidedAt { get; set; }

        public BusinessRequest? BusinessRequest { get; set; }
    }
}
