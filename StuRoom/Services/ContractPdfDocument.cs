using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StuRoom.Models;

namespace StuRoom.Services;

public class ContractPdfDocument(Contract contract) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Trang ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }

    void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
                .Bold().FontSize(13);
            col.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc")
                .Bold().FontSize(12);
            col.Item().AlignCenter().Text("━━━━━━━━━━━━━━━━━━━━━")
                .FontSize(10);
            col.Item().Height(12);
            col.Item().AlignCenter().Text("HỢP ĐỒNG THUÊ PHÒNG TRỌ")
                .Bold().FontSize(15);
            col.Item().AlignCenter().Text($"Số: {contract.Id:D4}/{DateTime.Now.Year}/HĐTT")
                .FontSize(10).FontColor(Colors.Grey.Medium);
            col.Item().Height(8);
        });
    }

    void ComposeContent(IContainer container)
    {
        var room    = contract.Room;
        var bldg    = room.Building;
        var tenant  = contract.Tenant;

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

        container.Column(col =>
        {
            // Preamble
            col.Item().Text($"Hôm nay ngày {DateTime.Now.Day:D2} tháng {DateTime.Now.Month:D2} năm {DateTime.Now.Year}; tại địa chỉ: {bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}.");
            col.Item().Height(4);
            col.Item().Text("Chúng tôi gồm:").Bold();
            col.Item().Height(6);

            // Bên A
            SectionTitle(col, "1. Đại diện bên cho thuê phòng trọ (Bên A):");
            InfoRow(col, "Ông/bà", bldg.Landlord?.FullName ?? bldg.Name);
            InfoRow(col, "Sinh ngày", bldg.Landlord?.DateOfBirth?.ToString("dd/MM/yyyy") ?? "..........................................");
            InfoRow(col, "Nơi đăng ký HK", "..........................................................................................");
            InfoRow(col, "CMND số", "..........................................");
            InfoRow(col, "Số điện thoại", bldg.Landlord?.PhoneNumber ?? "..........................................");
            col.Item().Height(8);

            // Bên B
            SectionTitle(col, "2. Bên thuê phòng trọ (Bên B):");
            InfoRow(col, "Ông/bà", tenant.FullName ?? tenant.UserName ?? "—");
            InfoRow(col, "Sinh ngày", tenant.DateOfBirth?.ToString("dd/MM/yyyy") ?? "..........................................");
            InfoRow(col, "Nơi đăng ký HK", "..........................................................................................");
            InfoRow(col, "Số CMND", "..........................................");
            InfoRow(col, "Số điện thoại", tenant.PhoneNumber ?? "..........................................");
            col.Item().Height(8);

            col.Item().Text("Sau khi bàn bạc trên tinh thần dân chủ, hai bên cùng có lợi, cùng thống nhất như sau:");
            col.Item().Height(8);

            // Điều khoản thỏa thuận
            SectionTitle(col, "ĐIỀU KHOẢN THỎA THUẬN");
            Clause(col, "-", $"Bên A đồng ý cho bên B thuê 01 phòng ở tại địa chỉ: Phòng {room.RoomNumber}, thuộc tòa nhà {bldg.Name}, {bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}.");
            Clause(col, "-", $"Giá thuê: {contract.MonthlyRent:N0} đ/tháng.");
            Clause(col, "-", "Hình thức thanh toán: Thanh toán vào đầu các tháng (Chuyển khoản hoặc Tiền mặt).");
            Clause(col, "-", $"Tiền điện: {elecStr} tính theo chỉ số công tơ, thanh toán vào cuối các tháng.");
            Clause(col, "-", $"Tiền nước: {waterStr} thanh toán vào đầu các tháng.");
            Clause(col, "-", $"Tiền đặt cọc: {contract.DepositAmount:N0} đ.");
            Clause(col, "-", $"Thời hạn hợp đồng: Kể từ ngày {contract.StartDate:dd/MM/yyyy} đến ngày {(contract.EndDate.HasValue ? contract.EndDate.Value.ToString("dd/MM/yyyy") : "Không xác định")}.");
            col.Item().Height(8);

            // Trách nhiệm
            SectionTitle(col, "TRÁCH NHIỆM CỦA CÁC BÊN");
            col.Item().Text("* Trách nhiệm của bên A:").Bold();
            Clause(col, "-", "Tạo mọi điều kiện thuận lợi để bên B thực hiện theo hợp đồng.");
            Clause(col, "-", "Cung cấp nguồn điện, nước, wifi cho bên B sử dụng.");
            col.Item().Height(4);
            col.Item().Text("* Trách nhiệm của bên B:").Bold();
            Clause(col, "-", "Thanh toán đầy đủ các khoản tiền theo đúng thỏa thuận.");
            Clause(col, "-", "Bảo quản các trang thiết bị và cơ sở vật chất của bên A trang bị cho ban đầu (làm hỏng phải sửa, mất phải đền).");
            Clause(col, "-", "Không được tự ý sửa chữa, cải tạo cơ sở vật chất khi chưa được sự đồng ý của bên A.");
            Clause(col, "-", "Giữ gìn vệ sinh trong và ngoài khuôn viên của phòng trọ.");
            Clause(col, "-", "Bên B phải chấp hành mọi quy định của pháp luật Nhà nước và quy định của địa phương.");
            Clause(col, "-", "Nếu bên B cho khách ở qua đêm thì phải báo và được sự đồng ý của chủ nhà đồng thời phải chịu trách nhiệm về các hành vi vi phạm pháp luật của khách trong thời gian ở lại.");
            col.Item().Height(8);

            // Trách nhiệm chung
            SectionTitle(col, "TRÁCH NHIỆM CHUNG");
            Clause(col, "-", "Hai bên phải tạo điều kiện cho nhau thực hiện hợp đồng.");
            Clause(col, "-", "Trong thời gian hợp đồng còn hiệu lực nếu bên nào vi phạm các điều khoản đã thỏa thuận thì bên còn lại có quyền đơn phương chấm dứt hợp đồng; nếu sự vi phạm hợp đồng đó gây tổn thất cho bên bị vi phạm hợp đồng thì bên vi phạm hợp đồng phải bồi thường thiệt hại.");
            Clause(col, "-", "Một trong hai bên muốn chấm dứt hợp đồng trước thời hạn thì phải báo trước cho bên kia ít nhất 30 ngày và hai bên phải có sự thống nhất.");
            Clause(col, "-", "Bên A phải trả lại tiền đặt cọc cho bên B.");
            Clause(col, "-", "Bên nào vi phạm điều khoản chung thì phải chịu trách nhiệm trước pháp luật.");
            Clause(col, "-", "Hợp đồng được lập thành 02 bản có giá trị pháp lý như nhau, mỗi bên giữ một bản.");
            col.Item().Height(8);

            if (!string.IsNullOrWhiteSpace(contract.Notes))
            {
                SectionTitle(col, "GHI CHÚ THÊM");
                col.Item().Text(contract.Notes);
                col.Item().Height(8);
            }

            // Ký tên
            col.Item().Height(16);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().AlignCenter().Text("BÊN B").Bold();
                    sig.Item().AlignCenter().Text("(Người thuê)").FontSize(9).FontColor(Colors.Grey.Medium);
                    sig.Item().Height(40);
                    sig.Item().AlignCenter().Text(tenant.FullName ?? "—").FontSize(9);
                });
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().AlignCenter().Text("BÊN A").Bold();
                    sig.Item().AlignCenter().Text("(Chủ trọ)").FontSize(9).FontColor(Colors.Grey.Medium);
                    sig.Item().Height(40);
                    sig.Item().AlignCenter().Text(bldg.Landlord?.FullName ?? bldg.Name).FontSize(9);
                });
            });
        });
    }

    static void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().Text(title).Bold().FontSize(12);
        col.Item().Height(4);
    }

    static void InfoRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(140).Text($"  {label}:").FontColor(Colors.Grey.Darken2);
            row.RelativeItem().Text(value).Bold();
        });
    }

    static void Clause(ColumnDescriptor col, string num, string text)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(25).Text(num).Bold();
            row.RelativeItem().Text(text);
        });
    }
}
