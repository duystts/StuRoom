using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;

namespace StuRoom.Services;

/// <summary>
/// Background service chạy mỗi 1 giờ, quét ViewingRequest sắp diễn ra trong 24h tới
/// và gửi email nhắc nhở cho cả Tenant lẫn Landlord.
/// </summary>
public class ViewingReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<ViewingReminderService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[VIEWING-REMINDER] Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VIEWING-REMINDER] Error processing reminders.");
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

        var now = DateTime.Now;
        var cutoff = now.AddHours(24);

        // Tìm các lịch hẹn Confirmed hoặc Pending, chưa gửi nhắc,
        // có thời gian hẹn trong vòng 24h tới
        var upcomingViewings = await db.ViewingRequests
            .Include(v => v.Tenant)
            .Include(v => v.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Where(v => !v.ReminderSent
                && (v.Status == ViewingStatus.Confirmed || v.Status == ViewingStatus.Pending)
                && (v.ConfirmedTime ?? v.ProposedTime) > now
                && (v.ConfirmedTime ?? v.ProposedTime) <= cutoff)
            .ToListAsync(ct);

        if (upcomingViewings.Count == 0)
        {
            logger.LogInformation("[VIEWING-REMINDER] No upcoming viewings to remind.");
            return;
        }

        logger.LogInformation("[VIEWING-REMINDER] Found {Count} viewing(s) to remind.", upcomingViewings.Count);

        foreach (var v in upcomingViewings)
        {
            var scheduledTime = v.ConfirmedTime ?? v.ProposedTime;
            var roomInfo = $"{v.Room.RoomNumber} — {v.Room.Building.Name}";
            var landlord = v.Room.Building.Landlord;

            // ── Email cho Tenant ──────────────────────────────────────
            try
            {
                await emailSender.SendEmailAsync(
                    v.Tenant.Email!,
                    "Nhắc lịch xem phòng ngày mai — StuRoom",
                    BuildTenantReminderHtml(v.Tenant.FullName, roomInfo,
                        v.Room.Building.Address, scheduledTime, landlord.FullName));

                logger.LogInformation("[VIEWING-REMINDER] Email sent to Tenant {Email} for Viewing #{Id}.",
                    v.Tenant.Email, v.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VIEWING-REMINDER] Failed to email Tenant {Email}.", v.Tenant.Email);
            }

            // ── Email cho Landlord ────────────────────────────────────
            try
            {
                await emailSender.SendEmailAsync(
                    landlord.Email!,
                    "Nhắc lịch khách xem phòng ngày mai — StuRoom",
                    BuildLandlordReminderHtml(landlord.FullName, roomInfo,
                        scheduledTime, v.Tenant.FullName));

                logger.LogInformation("[VIEWING-REMINDER] Email sent to Landlord {Email} for Viewing #{Id}.",
                    landlord.Email, v.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VIEWING-REMINDER] Failed to email Landlord {Email}.", landlord.Email);
            }

            // ── In-app Notification ───────────────────────────────────
            await notifier.SendAsync(v.TenantId, NotificationType.ViewingReminder,
                "Nhắc lịch xem phòng",
                $"Bạn có lịch xem phòng {roomInfo} vào lúc {scheduledTime:dd/MM/yyyy HH:mm}. Đừng quên nhé!",
                "ViewingRequest", v.Id);

            await notifier.SendAsync(landlord.Id, NotificationType.ViewingReminder,
                "Nhắc lịch khách xem phòng",
                $"Khách {v.Tenant.FullName} sẽ đến xem phòng {roomInfo} vào lúc {scheduledTime:dd/MM/yyyy HH:mm}.",
                "ViewingRequest", v.Id);

            // ── Đánh dấu đã gửi ──────────────────────────────────────
            v.ReminderSent = true;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("[VIEWING-REMINDER] Marked {Count} viewing(s) as reminded.", upcomingViewings.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Email HTML templates
    // ═══════════════════════════════════════════════════════════════════

    public static string BuildTenantReminderHtml(string tenantName, string roomInfo,
        string address, DateTime scheduledTime, string landlordName)
    {
        return $"""
        <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#f8f9fa;padding:32px;border-radius:12px;">
            <div style="background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:#fff;padding:24px 32px;border-radius:12px 12px 0 0;text-align:center;">
                <h1 style="margin:0;font-size:22px;">🔔 Nhắc lịch xem phòng</h1>
            </div>
            <div style="background:#fff;padding:24px 32px;border-radius:0 0 12px 12px;border:1px solid #e9ecef;border-top:none;">
                <p style="font-size:15px;color:#333;">Xin chào <strong>{tenantName}</strong>,</p>
                <p style="font-size:15px;color:#333;">Bạn có lịch hẹn xem phòng sắp tới:</p>
                <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                    <tr><td style="padding:8px 12px;color:#666;width:140px;">📍 Phòng</td><td style="padding:8px 12px;font-weight:600;color:#333;">{roomInfo}</td></tr>
                    <tr style="background:#f8f9fa;"><td style="padding:8px 12px;color:#666;">📌 Địa chỉ</td><td style="padding:8px 12px;color:#333;">{address}</td></tr>
                    <tr><td style="padding:8px 12px;color:#666;">🕐 Thời gian</td><td style="padding:8px 12px;font-weight:600;color:#764ba2;">{scheduledTime:dd/MM/yyyy HH:mm}</td></tr>
                    <tr style="background:#f8f9fa;"><td style="padding:8px 12px;color:#666;">👤 Chủ trọ</td><td style="padding:8px 12px;color:#333;">{landlordName}</td></tr>
                </table>
                <p style="font-size:14px;color:#666;">Vui lòng đến đúng giờ. Nếu cần thay đổi, hãy huỷ lịch trên hệ thống trước nhé!</p>
                <hr style="border:none;border-top:1px solid #eee;margin:20px 0;">
                <p style="font-size:12px;color:#999;text-align:center;">Email tự động từ StuRoom — Quản lý phòng trọ sinh viên</p>
            </div>
        </div>
        """;
    }

    public static string BuildLandlordReminderHtml(string landlordName, string roomInfo,
        DateTime scheduledTime, string tenantName)
    {
        return $"""
        <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#f8f9fa;padding:32px;border-radius:12px;">
            <div style="background:linear-gradient(135deg,#f093fb 0%,#f5576c 100%);color:#fff;padding:24px 32px;border-radius:12px 12px 0 0;text-align:center;">
                <h1 style="margin:0;font-size:22px;">🔔 Nhắc lịch khách xem phòng</h1>
            </div>
            <div style="background:#fff;padding:24px 32px;border-radius:0 0 12px 12px;border:1px solid #e9ecef;border-top:none;">
                <p style="font-size:15px;color:#333;">Xin chào <strong>{landlordName}</strong>,</p>
                <p style="font-size:15px;color:#333;">Bạn có khách hẹn xem phòng sắp tới:</p>
                <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                    <tr><td style="padding:8px 12px;color:#666;width:140px;">📍 Phòng</td><td style="padding:8px 12px;font-weight:600;color:#333;">{roomInfo}</td></tr>
                    <tr style="background:#f8f9fa;"><td style="padding:8px 12px;color:#666;">🕐 Thời gian</td><td style="padding:8px 12px;font-weight:600;color:#f5576c;">{scheduledTime:dd/MM/yyyy HH:mm}</td></tr>
                    <tr><td style="padding:8px 12px;color:#666;">👤 Khách thuê</td><td style="padding:8px 12px;color:#333;">{tenantName}</td></tr>
                </table>
                <p style="font-size:14px;color:#666;">Vui lòng chuẩn bị phòng và đón khách đúng giờ!</p>
                <hr style="border:none;border-top:1px solid #eee;margin:20px 0;">
                <p style="font-size:12px;color:#999;text-align:center;">Email tự động từ StuRoom — Quản lý phòng trọ sinh viên</p>
            </div>
        </div>
        """;
    }
}
