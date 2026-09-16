namespace HdfcApproval.Api.Models
{
    public class BusinessRequest
    {
        public string RequestId { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string ModuleId { get; set; } = string.Empty;

        public string MakerId { get; set; } = string.Empty;

        public int Revision { get; set; }

        public string TemplateId { get; set; } = string.Empty;

        public string TemplateVersion { get; set; } = string.Empty;

        public string? SnapshotHash { get; set; }

        public string? WorkflowId { get; set; } = string.Empty;

        public string? ApprovalStatus { get; set; } = string.Empty;

        public string? ProcessingStatus { get; set; } = string.Empty;

        public string StateVersion { get; set; } = string.Empty;
    }
}
