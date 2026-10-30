namespace BankMvc.Models
{
    public class AuditEntryDto
    {
        public long AuditId { get; set; }
        public int AdminUserId { get; set; }
        public string AdminUsername { get; set; } = "";
        public string Action { get; set; } = "";
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Details { get; set; }

        public DateTime Timestamp { get; set; }
    }

    public class AuditQuery
    {
        // set by the controller from the login cookie, never taken from the URL
        public int? AdminUserId { get; set; }

        public string? Search { get; set; }

        public string? ActionType { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string SortDir { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AdminDashboardViewModel
    {
        public AuditQuery Query { get; set; } = new();

        public PagedResult<AuditEntryDto>? Audit { get; set; }

        public int? TotalTransactions { get; set; }

        public string? Error { get; set; }
    }

    public class HomeViewModel
    {
        public bool ServiceUp { get; set; } = true;
    }

    public class StatusViewModel
    {
        public int Code { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
