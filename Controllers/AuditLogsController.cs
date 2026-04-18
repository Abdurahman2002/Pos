using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryApprovePolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class AuditLogsController : Controller
    {
        private readonly IUnitOfWork<AuditLog> _auditLogs;

        public AuditLogsController(IUnitOfWork<AuditLog> auditLogs)
        {
            _auditLogs = auditLogs;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _auditLogs.Repository.GetAll();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    (a.CreatedByUserName != null && a.CreatedByUserName.Contains(term)) ||
                    (a.EntityType != null && a.EntityType.Contains(term)) ||
                    (a.EntityNumber != null && a.EntityNumber.Contains(term)) ||
                    (a.Action != null && a.Action.Contains(term)) ||
                    (a.Description != null && a.Description.Contains(term))
                );
            }

            var list = await query
                .OrderByDescending(a => a.Created)
                .Take(200)
                .ToListAsync();

            ViewBag.Search = search;
            return View(list);
        }
    }
}
