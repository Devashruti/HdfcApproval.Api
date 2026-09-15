using HdfcApproval.Api.Temporal.Models;

namespace HdfcApproval.Api.Models
{
    public class OutboxEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string AggregateId { get; set; } = string.Empty;
        public string PayloadRef { get; set; } = string.Empty;
        public string DeliveryState { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
    }
}
