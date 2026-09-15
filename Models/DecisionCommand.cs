namespace HdfcApproval.Api.Models
{
    public class DecisionCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string ExpectedVersion { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; }
        public string StoredResult { get; set; } = string.Empty;
    }
}
