namespace HdfcApproval.Api.Models
{
    public class Employeedata
    {
        public string RequestId { get; set; }

        public string EmployeeCode { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public int DepartmentId { get; set; }

        public decimal Salary { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string SalaryPeriod { get; set; } = string.Empty;

        public DateTime? JoiningDate { get; set; }

        public BusinessRequest? BusinessRequest { get; set; }
    }
}
