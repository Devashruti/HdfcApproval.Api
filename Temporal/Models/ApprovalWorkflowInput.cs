namespace HdfcApproval.Api.Temporal.Models
{
    public class ApprovalWorkflowInput
    {
        public string RequestId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string TenantId { get; set; } = string.Empty;
    }
}
