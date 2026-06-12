using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StuRoom.Data;
using StuRoom.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StuRoom.Services;

/// <summary>
/// Background service chạy mỗi 12 giờ, quét các hợp đồng sắp hết hạn (trong vòng 30 ngày)
/// và các hóa đơn sắp đến hạn thanh toán (trong vòng 3 ngày) để gửi email & thông báo nhắc nhở.
/// </summary>
public class RentalReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<RentalReminderService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[RENTAL-REMINDER] Hosted Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[RENTAL-REMINDER] Error occurred processing reminders.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateTime.Today;
        var expiryCutoff = today.AddDays(30);
        var dueCutoff = today.AddDays(3);

        // ── 1. NHẮC NHỞ HỢP ĐỒNG SẮP HẾT HẠN (Trước 30 ngày) ──────────────────
        var expiringContracts = await db.Contracts
            .Include(c => c.Tenant)
            .Include(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Where(c => c.Status == ContractStatus.Active
                && !c.ExpiryReminderSent
                && c.EndDate != null
                && c.EndDate.Value.Date >= today
                && c.EndDate.Value.Date <= expiryCutoff)
            .ToListAsync(ct);

        if (expiringContracts.Any())
        {
            logger.LogInformation("[RENTAL-REMINDER] Found {Count} contract(s) expiring in 30 days.", expiringContracts.Count);
            foreach (var c in expiringContracts)
            {
                var roomInfo = $"phòng {c.Room.RoomNumber} tại {c.Room.Building.Name}";
                var endDateStr = c.EndDate!.Value.ToString("dd/MM/yyyy");

                // Gửi thông báo in-app cho Tenant
                await notifier.SendAsync(c.TenantId, NotificationType.ContractExpiring,
                    "Hợp đồng thuê phòng sắp hết hạn",
                    $"Hợp đồng của bạn cho {roomInfo} sẽ hết hạn vào ngày {endDateStr}. Vui lòng liên hệ chủ trọ để thực hiện gia hạn.",
                    "Contract", c.Id);

                // Gửi thông báo in-app cho Landlord
                await notifier.SendAsync(c.Room.Building.LandlordId, NotificationType.ContractExpiring,
                    "Hợp đồng thuê sắp hết hạn",
                    $"Hợp đồng của người thuê {c.Tenant.FullName} tại {roomInfo} sẽ hết hạn vào ngày {endDateStr}.",
                    "Contract", c.Id);

                // Email cho Tenant
                try
                {
                    await emailSender.SendEmailAsync(c.Tenant.Email!,
                        "Hợp đồng thuê phòng sắp hết hạn — StuRoom",
                        $"Xin chào <strong>{c.Tenant.FullName}</strong>,<br><br>" +
                        $"Hợp đồng thuê <strong>{roomInfo}</strong> của bạn sẽ hết hạn vào ngày <strong>{endDateStr}</strong>.<br>" +
                        $"Vui lòng liên hệ với chủ trọ <strong>{c.Room.Building.Landlord.FullName}</strong> để thảo luận và tiến hành gia hạn hợp đồng nếu có nhu cầu ở tiếp.<br><br>" +
                        "Trân trọng cảm ơn!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[RENTAL-REMINDER] Failed to send expiry email to tenant: {Email}", c.Tenant.Email);
                }

                // Email cho Landlord
                try
                {
                    await emailSender.SendEmailAsync(c.Room.Building.Landlord.Email!,
                        "Hợp đồng thuê phòng sắp hết hạn — StuRoom",
                        $"Xin chào <strong>{c.Room.Building.Landlord.FullName}</strong>,<br><br>" +
                        $"Hợp đồng thuê <strong>{roomInfo}</strong> ký với <strong>{c.Tenant.FullName}</strong> sẽ hết hạn vào ngày <strong>{endDateStr}</strong>.<br>" +
                        $"Vui lòng chủ động trao đổi với người thuê để tiến hành gia hạn hoặc làm thủ tục bàn giao phòng.<br><br>" +
                        "Trân trọng cảm ơn!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[RENTAL-REMINDER] Failed to send expiry email to landlord: {Email}", c.Room.Building.Landlord.Email);
                }

                // Đánh dấu đã nhắc
                c.ExpiryReminderSent = true;
            }
        }

        // ── 2. NHẮC NHỞ HÓA ĐƠN SẮP ĐẾN HẠN (Trước 3 ngày) ─────────────────────
        var dueInvoices = await db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building)
            .Where(i => (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue)
                && !i.DueReminderSent
                && i.DueDate.Date >= today
                && i.DueDate.Date <= dueCutoff)
            .ToListAsync(ct);

        if (dueInvoices.Any())
        {
            logger.LogInformation("[RENTAL-REMINDER] Found {Count} invoice(s) due in 3 days.", dueInvoices.Count);
            foreach (var i in dueInvoices)
            {
                var tenant = i.Contract.Tenant;
                var room = i.Contract.Room;
                var roomInfo = $"phòng {room.RoomNumber} tại {room.Building.Name}";
                var dueDateStr = i.DueDate.ToString("dd/MM/yyyy");

                // Gửi thông báo in-app cho Tenant
                await notifier.SendAsync(tenant.Id, NotificationType.InvoiceDue,
                    "Nhắc nhở hạn đóng tiền phòng",
                    $"Hóa đơn tháng {i.BillingMonth}/{i.BillingYear} ({roomInfo}) sắp đến hạn đóng tiền vào ngày {dueDateStr}. Số tiền cần thanh toán: {i.TotalAmount:N0} ₫.",
                    "Invoice", i.Id);

                // Email cho Tenant
                try
                {
                    await emailSender.SendEmailAsync(tenant.Email!,
                        "Nhắc nhở hạn nộp tiền phòng trọ — StuRoom",
                        $"Xin chào <strong>{tenant.FullName}</strong>,<br><br>" +
                        $"Hệ thống gửi thông báo nhắc bạn nộp tiền phòng của tháng <strong>{i.BillingMonth}/{i.BillingYear}</strong>.<br>" +
                        $"Hạn chót thanh toán là ngày: <strong>{dueDateStr}</strong>.<br>" +
                        $"Số tiền cần thanh toán: <strong>{i.TotalAmount:N0} ₫</strong>.<br><br>" +
                        $"Bạn có thể truy cập vào StuRoom tại trang cá nhân -> <strong>Phòng đang thuê</strong> để thực hiện thanh toán trực tuyến nhanh chóng.<br>" +
                        $"Vui lòng đóng tiền đúng hạn để tránh phát sinh chi phí trễ hạn. Trân trọng!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[RENTAL-REMINDER] Failed to send invoice due email to tenant: {Email}", tenant.Email);
                }

                // Đánh dấu đã nhắc
                i.DueReminderSent = true;
            }
        }

        // Lưu thay đổi vào CSDL
        if (expiringContracts.Any() || dueInvoices.Any())
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[RENTAL-REMINDER] Processed reminders and updated database.");
        }
        else
        {
            logger.LogInformation("[RENTAL-REMINDER] No reminders were sent in this run.");
        }
    }
}
