namespace HdfcApproval.Api.Models
{
    public class AuditEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string StageInstanceId { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string FromState { get; set; } = string.Empty;
        public string ToState { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string CommandId { get; set; } = string.Empty;
    }
}
