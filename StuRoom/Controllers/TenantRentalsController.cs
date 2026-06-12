using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StuRoom.Controllers;

[Authorize(Policy = "TenantOnly")]
public class TenantRentalsController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    INotificationService notifier,
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender emailSender) : Controller
{
    private string CurrentUserId => userManager.GetUserId(User)!;

    // ── DANH SÁCH PHÒNG VÀ HỢP ĐỒNG ĐANG THUÊ ─────────────────────────
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;

        var contracts = await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(c => c.Room.Images)
            .Include(c => c.Tenant)
            .Include(c => c.Members).ThenInclude(m => m.Tenant)
            .Where(c => c.TenantId == userId || c.Members.Any(m => m.TenantId == userId && m.LeaveDate == null))
            .OrderByDescending(c => c.Status == ContractStatus.Active)
            .ThenByDescending(c => c.StartDate)
            .ToListAsync();

        return View(contracts);
    }

    // ── CHI TIẾT HỢP ĐỒNG THUÊ PHÒNG ──────────────────────────────────
    public async Task<IActionResult> ContractDetail(int id)
    {
        var userId = CurrentUserId;

        var contract = await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(c => c.Tenant)
            .Include(c => c.Members).ThenInclude(m => m.Tenant)
            .Include(c => c.Invoices.Where(i => i.Status != InvoiceStatus.Draft))
            .FirstOrDefaultAsync(c => c.Id == id && 
                (c.TenantId == userId || c.Members.Any(m => m.TenantId == userId && m.LeaveDate == null)));

        if (contract == null) return NotFound();

        return View(contract);
    }

    // ── CHI TIẾT HÓA ĐƠN THANH TOÁN ──────────────────────────────────
    public async Task<IActionResult> InvoiceDetail(int id)
    {
        var userId = CurrentUserId;

        var invoice = await db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Members)
            .FirstOrDefaultAsync(i => i.Id == id && 
                (i.Contract.TenantId == userId || i.Contract.Members.Any(m => m.TenantId == userId && m.LeaveDate == null)));

        if (invoice == null) return NotFound();
        if (invoice.Status == InvoiceStatus.Draft) return NotFound();

        return View(invoice);
    }

    // ── MÔ PHỎNG THANH TOÁN HÓA ĐƠN ───────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayInvoice(int id, PaymentMethod method, string? notes)
    {
        var userId = CurrentUserId;

        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .FirstOrDefaultAsync(i => i.Id == id && 
                (i.Contract.TenantId == userId || i.Contract.Members.Any(m => m.TenantId == userId && m.LeaveDate == null)));

        if (invoice == null) return NotFound();

        if (invoice.Status == InvoiceStatus.Paid || invoice.Status == InvoiceStatus.Cancelled || invoice.Status == InvoiceStatus.Draft)
        {
            TempData["Error"] = "Hóa đơn này không thể thanh toán (đã thanh toán hoặc chưa được gửi).";
            return RedirectToAction(nameof(InvoiceDetail), new { id });
        }

        var totalPaid = invoice.Payments.Sum(p => p.Amount);
        var remaining = invoice.TotalAmount - totalPaid;

        if (remaining <= 0)
        {
            invoice.Status = InvoiceStatus.Paid;
            await db.SaveChangesAsync();
            TempData["Info"] = "Hóa đơn đã được thanh toán đầy đủ.";
            return RedirectToAction(nameof(InvoiceDetail), new { id });
        }

        var randomRef = $"SIM-{method.ToString().ToUpper()}-{new Random().Next(100000, 999999)}";
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = remaining,
            PaymentDate = DateTime.Now,
            Method = method,
            TransactionRef = randomRef,
            Notes = string.IsNullOrWhiteSpace(notes) ? "Tenant thanh toán trực tuyến qua hệ thống StuRoom" : notes.Trim(),
            RecordedById = userId
        };

        db.Payments.Add(payment);
        invoice.Status = InvoiceStatus.Paid;
        await db.SaveChangesAsync();

        var landlordId = invoice.Contract.Room.Building.LandlordId;
        var landlordEmail = invoice.Contract.Room.Building.Landlord.Email;
        var title = "Nhận khoản thanh toán mới";
        var content = $"Tenant {invoice.Contract.Tenant.FullName} đã thanh toán hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} phòng {invoice.Contract.Room.RoomNumber} ({remaining:N0} ₫).";

        await notifier.SendAsync(landlordId, NotificationType.PaymentReceived, title, content, "Invoice", invoice.Id);

        if (!string.IsNullOrEmpty(landlordEmail))
        {
            try
            {
                await emailSender.SendEmailAsync(landlordEmail, $"[StuRoom] Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã được thanh toán",
                    $"Chào chủ trọ <strong>{invoice.Contract.Room.Building.Landlord.FullName}</strong>,<br><br>" +
                    $"Hóa đơn tháng <strong>{invoice.BillingMonth}/{invoice.BillingYear}</strong> của phòng <strong>{invoice.Contract.Room.RoomNumber}</strong> " +
                    $"đã được thanh toán bởi <strong>{invoice.Contract.Tenant.FullName}</strong> qua phương thức <strong>{method}</strong>.<br>" +
                    $"Số tiền thanh toán: <strong>{remaining:N0} ₫</strong>.<br>" +
                    $"Mã giao dịch: <strong>{randomRef}</strong>.<br><br>" +
                    $"Vui lòng kiểm tra tài khoản của bạn. Trân trọng!");
            }
            catch
            {
            }
        }

        TempData["Success"] = $"Thanh toán trực tuyến thành công số tiền {remaining:N0} ₫ qua cổng {method}!";
        return RedirectToAction(nameof(InvoiceDetail), new { id });
    }

    // ── XUẤT HỢP ĐỒNG PDF ─────────────────────────────────────────────
    public async Task<IActionResult> ExportContractPdf(int id)
    {
        var contract = await GetMyContractFull(id);
        if (contract == null) return NotFound();

        QuestPDF.Settings.License = LicenseType.Community;
        var doc = new ContractPdfDocument(contract);
        var bytes = doc.GeneratePdf();
        return File(bytes, "application/pdf", $"HopDong_{contract.Id:D4}.pdf");
    }

    // ── XUẤT HỢP ĐỒNG WORD ────────────────────────────────────────────
    public async Task<IActionResult> ExportContractDocx(int id)
    {
        var contract = await GetMyContractFull(id);
        if (contract == null) return NotFound();

        using var ms = new MemoryStream();
        BuildWordDocument(contract, ms);
        ms.Position = 0;
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"HopDong_{contract.Id:D4}.docx");
    }

    private async Task<Contract?> GetMyContractFull(int id)
    {
        var userId = CurrentUserId;
        return await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(c => c.Tenant)
            .Include(c => c.Members).ThenInclude(m => m.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id && 
                (c.TenantId == userId || c.Members.Any(m => m.TenantId == userId && m.LeaveDate == null)));
    }

    private static void BuildWordDocument(Contract contract, Stream stream)
    {
        var room = contract.Room;
        var bldg = room.Building;
        var tenant = contract.Tenant;

        // Fetch electricity and water fee configurations
        var electricityFee = room.FeeConfigs.FirstOrDefault(f => f.FeeCategory == FeeCategory.Electricity && f.IsActive)
            ?? bldg.FeeConfigs.FirstOrDefault(f => f.FeeCategory == FeeCategory.Electricity && f.IsActive);

        var waterFee = room.FeeConfigs.FirstOrDefault(f => f.FeeCategory == FeeCategory.Water && f.IsActive)
            ?? bldg.FeeConfigs.FirstOrDefault(f => f.FeeCategory == FeeCategory.Water && f.IsActive);

        string elecStr = electricityFee != null
            ? $"{electricityFee.UnitPrice:N0} đ/{electricityFee.Unit}"
            : ".................. đ/kwh";

        string waterStr = waterFee != null
            ? $"{waterFee.UnitPrice:N0} đ/{waterFee.Unit}"
            : ".................. đ/người";

        using var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new WpDocument();
        var body = mainPart.Document.AppendChild(new Body());

        var sectPr = new SectionProperties(
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134 });
        body.AppendChild(sectPr);

        static Paragraph CenteredPara(string text, bool bold = false, int fontSize = 24)
        {
            var rp = new RunProperties(new FontSize { Val = fontSize.ToString() });
            if (bold) { rp.AppendChild(new Bold()); rp.AppendChild(new BoldComplexScript()); }
            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(rp, new Text(text)));
        }

        static Paragraph ParagraphWithText(string text, int fontSize = 22)
            => new(new Run(new RunProperties(new FontSize { Val = fontSize.ToString() }),
                   new Text(text)));

        static Paragraph InfoPara(string label, string value)
            => new(new Run(
                   new RunProperties(new FontSize { Val = "22" }),
                   new Text($"    {label}: ")),
               new Run(
                   new RunProperties(new Bold(), new BoldComplexScript(),
                       new FontSize { Val = "22" }),
                   new Text(value)));

        static Paragraph HeadingPara(string text)
            => new(new Run(
                   new RunProperties(new Bold(), new BoldComplexScript(),
                       new FontSize { Val = "24" }),
                   new Text(text)));

        // Header
        body.AppendChild(CenteredPara("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, 26));
        body.AppendChild(CenteredPara("Độc lập - Tự do - Hạnh phúc", true, 24));
        body.AppendChild(CenteredPara("━━━━━━━━━━━━━━━━━━━━━", false, 20));
        body.AppendChild(CenteredPara("HỢP ĐỒNG THUÊ PHÒNG TRỌ", true, 28));
        body.AppendChild(CenteredPara($"Số: {contract.Id:D4}/{DateTime.Now.Year}/HĐTT", false, 20));
        body.AppendChild(ParagraphWithText(""));

        // Preamble
        body.AppendChild(ParagraphWithText($"Hôm nay ngày {DateTime.Now.Day:D2} tháng {DateTime.Now.Month:D2} năm {DateTime.Now.Year}; tại địa chỉ: {bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}."));
        body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold(), new BoldComplexScript(), new FontSize { Val = "22" }), new Text("Chúng tôi gồm:"))));
        body.AppendChild(ParagraphWithText(""));

        // Bên A
        body.AppendChild(HeadingPara("1. Đại diện bên cho thuê phòng trọ (Bên A):"));
        body.AppendChild(InfoPara("Ông/bà", bldg.Landlord?.FullName ?? bldg.Name));
        body.AppendChild(InfoPara("Sinh ngày", bldg.Landlord?.DateOfBirth?.ToString("dd/MM/yyyy") ?? ".........................................."));
        body.AppendChild(InfoPara("Nơi đăng ký HK", ".........................................................................................."));
        body.AppendChild(InfoPara("CMND số", ".........................................."));
        body.AppendChild(InfoPara("Số điện thoại", bldg.Landlord?.PhoneNumber ?? ".........................................."));
        body.AppendChild(ParagraphWithText(""));

        // Bên B
        body.AppendChild(HeadingPara("2. Bên thuê phòng trọ (Bên B):"));
        body.AppendChild(InfoPara("Ông/bà", tenant.FullName ?? tenant.UserName ?? "—"));
        body.AppendChild(InfoPara("Sinh ngày", tenant.DateOfBirth?.ToString("dd/MM/yyyy") ?? ".........................................."));
        body.AppendChild(InfoPara("Nơi đăng ký HK", ".........................................................................................."));
        body.AppendChild(InfoPara("Số CMND", ".........................................."));
        body.AppendChild(InfoPara("Số điện thoại", tenant.PhoneNumber ?? ".........................................."));
        body.AppendChild(ParagraphWithText(""));

        body.AppendChild(ParagraphWithText("Sau khi bàn bạc trên tinh thần dân chủ, hai bên cùng có lợi, cùng thống nhất như sau:"));
        body.AppendChild(ParagraphWithText(""));

        // Điều khoản thỏa thuận
        body.AppendChild(HeadingPara("ĐIỀU KHOẢN THỎA THUẬN"));
        body.AppendChild(ParagraphWithText($"- Bên A đồng ý cho bên B thuê 01 phòng ở tại địa chỉ: Phòng {room.RoomNumber}, thuộc tòa nhà {bldg.Name}, {bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}."));
        body.AppendChild(ParagraphWithText($"- Giá thuê: {contract.MonthlyRent:N0} đ/tháng."));
        body.AppendChild(ParagraphWithText("- Hình thức thanh toán: Thanh toán vào đầu các tháng (Chuyển khoản hoặc Tiền mặt)."));
        body.AppendChild(ParagraphWithText($"- Tiền điện: {elecStr} tính theo chỉ số công tơ, thanh toán vào cuối các tháng."));
        body.AppendChild(ParagraphWithText($"- Tiền nước: {waterStr} thanh toán vào đầu các tháng."));
        body.AppendChild(ParagraphWithText($"- Tiền đặt cọc: {contract.DepositAmount:N0} đ."));
        body.AppendChild(ParagraphWithText($"- Thời hạn hợp đồng: Kể từ ngày {contract.StartDate:dd/MM/yyyy} đến ngày {(contract.EndDate.HasValue ? contract.EndDate.Value.ToString("dd/MM/yyyy") : "Không xác định")}."));
        body.AppendChild(ParagraphWithText(""));

        // Trách nhiệm
        body.AppendChild(HeadingPara("TRÁCH NHIỆM CỦA CÁC BÊN"));
        body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold(), new BoldComplexScript(), new FontSize { Val = "22" }), new Text("* Trách nhiệm của bên A:"))));
        body.AppendChild(ParagraphWithText("- Tạo mọi điều kiện thuận lợi để bên B thực hiện theo hợp đồng."));
        body.AppendChild(ParagraphWithText("- Cung cấp nguồn điện, nước, wifi cho bên B sử dụng."));
        body.AppendChild(new Paragraph(new Run(new RunProperties(new Bold(), new BoldComplexScript(), new FontSize { Val = "22" }), new Text("* Trách nhiệm của bên B:"))));
        body.AppendChild(ParagraphWithText("- Thanh toán đầy đủ các khoản tiền theo đúng thỏa thuận."));
        body.AppendChild(ParagraphWithText("- Bảo quản các trang thiết bị và cơ sở vật chất của bên A trang bị cho ban đầu (làm hỏng phải sửa, mất phải đền)."));
        body.AppendChild(ParagraphWithText("- Không được tự ý sửa chữa, cải tạo cơ sở vật chất khi chưa được sự đồng ý của bên A."));
        body.AppendChild(ParagraphWithText("- Giữ gìn vệ sinh trong và ngoài khuôn viên của phòng trọ."));
        body.AppendChild(ParagraphWithText("- Bên B phải chấp hành mọi quy định của pháp luật Nhà nước và quy định của địa phương."));
        body.AppendChild(ParagraphWithText("- Nếu bên B cho khách ở qua đêm thì phải báo và được sự đồng ý của chủ nhà đồng thời phải chịu trách nhiệm về các hành vi vi phạm pháp luật của khách trong thời gian ở lại."));
        body.AppendChild(ParagraphWithText(""));

        // Trách nhiệm chung
        body.AppendChild(HeadingPara("TRÁCH NHIỆM CHUNG"));
        body.AppendChild(ParagraphWithText("- Hai bên phải tạo điều kiện cho nhau thực hiện hợp đồng."));
        body.AppendChild(ParagraphWithText("- Trong thời gian hợp đồng còn hiệu lực nếu bên nào vi phạm các điều khoản đã thỏa thuận thì bên còn lại có quyền đơn phương chấm dứt hợp đồng; nếu sự vi phạm hợp đồng đó gây tổn thất cho bên bị vi phạm hợp đồng thì bên vi phạm hợp đồng phải bồi thường thiệt hại."));
        body.AppendChild(ParagraphWithText("- Một trong hai bên muốn chấm dứt hợp đồng trước thời hạn thì phải báo trước cho bên kia ít nhất 30 ngày và hai bên phải có sự thống nhất."));
        body.AppendChild(ParagraphWithText("- Bên A phải trả lại tiền đặt cọc cho bên B."));
        body.AppendChild(ParagraphWithText("- Bên nào vi phạm điều khoản chung thì phải chịu trách nhiệm trước pháp luật."));
        body.AppendChild(ParagraphWithText("- Hợp đồng được lập thành 02 bản có giá trị pháp lý như nhau, mỗi bên giữ một bản."));
        body.AppendChild(ParagraphWithText(""));

        if (!string.IsNullOrWhiteSpace(contract.Notes))
        {
            body.AppendChild(HeadingPara("GHI CHÚ THÊM"));
            body.AppendChild(ParagraphWithText(contract.Notes));
            body.AppendChild(ParagraphWithText(""));
        }

        // Ký tên (2 cột dùng table)
        body.AppendChild(ParagraphWithText(""));
        var tbl = new Table(
            new TableProperties(
                new TableWidth { Width = "9638", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

        var sigRow = new TableRow();
        var cellA = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "4819", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "22" }), new Text("BÊN B"))),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text("(Người thuê)"))),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text(tenant.FullName ?? "—"))));

        var cellB = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "4819", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "22" }), new Text("BÊN A"))),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text("(Chủ trọ)"))),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text(bldg.Landlord?.FullName ?? bldg.Name))));

        sigRow.AppendChild(cellA);
        sigRow.AppendChild(cellB);
        tbl.AppendChild(sigRow);
        body.AppendChild(tbl);

        mainPart.Document.Save();
    }
}
