using HdfcApproval.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HdfcApproval.Api.Data
{
    public class ApprovalDbContext : DbContext

    {
        public ApprovalDbContext(
       DbContextOptions<ApprovalDbContext> options)
       : base(options)
        { }

        //public DbSet<DecisionCommand> DecisionCommands { get; set; }
        //public DbSet<AuditEvent> AuditEvents { get; set; }
        //public DbSet<OutboxEvent> OutboxEvents { get; set; }
        //public DbSet<Artifactjob> Artifactjobs { get; set; }

        public DbSet<BusinessRequest> BusinessRequests => Set<BusinessRequest>();

        public DbSet<Loandata> LoansData => Set<Loandata>();

        public DbSet<Employeedata> EmployeesData => Set<Employeedata>();

        public DbSet<Humantask> HumanTasks => Set<Humantask>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BusinessRequest>(entity =>
            {
                entity.ToTable("business_request");
                entity.HasKey(x => x.RequestId);
                entity.Property(x => x.RequestId)
                    .HasColumnName("request_id");
                entity.Property(x => x.TenantId)
                    .HasColumnName("tenant_id");
                entity.Property(x => x.ModuleId)
                    .HasColumnName("module_id");
                entity.Property(x => x.MakerId)
                    .HasColumnName("maker_id");
                entity.Property(x => x.Revision)
                    .HasColumnName("revision");
                entity.Property(x => x.TemplateId)
                    .HasColumnName("template_id");
                entity.Property(x => x.TemplateVersion)
                    .HasColumnName("template_version");
                entity.Property(x => x.SnapshotHash)
                    .HasColumnName("snapshot_hash");
                entity.Property(x => x.WorkflowId)
                    .HasColumnName("workflow_id");
                entity.HasIndex(x => x.WorkflowId)
                    .IsUnique();
                entity.Property(x => x.ApprovalStatus)
                    .HasColumnName("approval_status");
                entity.Property(x => x.ProcessingStatus)
                    .HasColumnName("processing_status");
                entity.Property(x => x.StateVersion)
                    .HasColumnName("state_version");

            });

            modelBuilder.Entity<Loandata>(entity =>
            {
                entity.ToTable("loan_data");
                entity.HasKey(x => x.RequestId);
                entity.Property(x => x.RequestId)
                    .HasColumnName("request_id");
                entity.Property(x => x.CustomerId)
                    .HasColumnName("customer_id");
                entity.Property(x => x.CustomerName)
                    .HasColumnName("customer_name");
                entity.Property(x => x.LoanAmount)
                    .HasColumnName("loan_amount")
                    .HasPrecision(10, 2);
                entity.Property(x => x.Currency)
                    .HasColumnName("currency");
                entity.Property(x => x.EncryptedPan)
                    .HasColumnName("encrypted_pan");
                entity.Property(x => x.Dob)
                .HasColumnName("dob");
                entity.Property(x => x.Mobile)
                    .HasColumnName("mobile_num");
                entity.HasOne(x => x.BusinessRequest)
                    .WithOne()
                    .HasForeignKey<Loandata>(x => x.RequestId);
            });

            modelBuilder.Entity<Employeedata>(entity =>
            {
                entity.ToTable("employee_data");
                entity.HasKey(x => x.RequestId);
                entity.Property(x => x.RequestId)
                    .HasColumnName("request_id");
                entity.Property(x => x.EmployeeCode)
                    .HasColumnName("employee_code");
                entity.Property(x => x.EmployeeName)
                    .HasColumnName("employee_name");
                entity.Property(x => x.DepartmentId)
                    .HasColumnName("department_id");
                entity.Property(x => x.Salary)
                    .HasColumnName("salary")
                    .HasPrecision(10, 2);
                entity.Property(x => x.Currency)
                    .HasColumnName("currency");
                entity.Property(x => x.SalaryPeriod)
                    .HasColumnName("salary_period");
                entity.Property(x => x.JoiningDate)
                    .HasColumnName("joining_date");
                entity.HasOne(x => x.BusinessRequest)
                    .WithOne()
                    .HasForeignKey<Employeedata>(x => x.RequestId);
            });

            modelBuilder.Entity<Humantask>(entity =>
            {
                entity.ToTable("human_task");
                entity.HasKey(x => x.TaskId);
                entity.Property(x => x.TaskId)
                    .HasColumnName("task_id");
                entity.Property(x => x.RequestId)
                    .HasColumnName("request_id");
                entity.Property(x => x.StageId)
                    .HasColumnName("stage_id");
                entity.Property(x=> x.StageInstanceId)
                    .HasColumnName("stage_instance_id");
                entity.HasIndex(x => x.StageInstanceId)
                    .IsUnique();
                entity.Property(x => x.Role)
                    .HasColumnName("Role");
                entity.Property(x => x.Status)
                    .HasColumnName("status");
                entity.Property(x => x.Version)
                    .HasColumnName("version");
                entity.Property(x => x.OpenedAt)
                    .HasColumnName("opened_at");
                entity.Property(x => x.DecidedBy)
                    .HasColumnName("decided_by");
                entity.Property(x => x.DecidedAt)
                    .HasColumnName("decided_at");
                entity.HasOne<BusinessRequest>()
                    .WithMany()
                    .HasForeignKey(x => x.RequestId);
            });
        }
    }
}
