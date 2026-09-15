namespace HdfcApproval.Api.Models
{
    public class Loandata
    {
        public string RequestId { get; set; }

        public string CustomerId { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public decimal LoanAmount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string? EncryptedPan { get; set; }

        public DateTime? Dob { get; set; }

        public string? Mobile { get; set; }

        public BusinessRequest? BusinessRequest { get; set; }
    }
}
