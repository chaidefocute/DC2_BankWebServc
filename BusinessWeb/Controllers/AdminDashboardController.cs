using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly BankApiClient _api;

        public AdminDashboardController(BankApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(AuditQuery query)
        {
            // an admin only sees their own actions, the ID comes from the login cookie so the URL can't change it
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
                return Forbid();
            query.AdminUserId = adminId;

            query.Page = Math.Max(1, query.Page);
            query.PageSize = new[] { 10, 20, 50 }.Contains(query.PageSize) ? query.PageSize : 20;
            query.SortDir = query.SortDir == "asc" ? "asc" : "desc";

            var vm = new AdminDashboardViewModel { Query = query };

            if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            {
                vm.Error = "The 'from' date can't be after the 'to' date.";
                return View(vm);
            }

            var audit = await _api.GetAuditLogAsync(query);
            if (audit.Ok)
                vm.Audit = audit.Data;
            else
                vm.Error = audit.Message;

            var tx = await _api.GetTransactionsAsync(new TransactionQuery { Page = 1, PageSize = 1 });
            if (tx.Ok && tx.Data != null)
                vm.TotalTransactions = tx.Data.TotalCount;

            return View(vm);
        }
    }
}
