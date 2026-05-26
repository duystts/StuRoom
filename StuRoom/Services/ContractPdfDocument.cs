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

        container.Column(col =>
        {
            // Căn cứ
            col.Item().Text(text =>
            {
                text.Span("Căn cứ ").Italic();
                text.Span("Bộ luật Dân sự năm 2015; Luật Nhà ở năm 2014 và các quy định pháp luật hiện hành.").Italic();
            });
            col.Item().Height(8);

            col.Item().Text("Hôm nay, các bên cùng thống nhất ký kết hợp đồng thuê phòng với các điều khoản sau:");
            col.Item().Height(12);

            // Bên A
            SectionTitle(col, "ĐIỀU 1 – BÊN CHO THUÊ (BÊN A)");
            InfoRow(col, "Họ và tên", bldg.Landlord?.FullName ?? bldg.Name);
            InfoRow(col, "Tòa nhà",   bldg.Name);
            InfoRow(col, "Địa chỉ",   bldg.Address + ", " + bldg.Ward + ", " + bldg.District + ", " + bldg.Province);
            col.Item().Height(8);

            // Bên B
            SectionTitle(col, "ĐIỀU 2 – BÊN THUÊ (BÊN B)");
            InfoRow(col, "Họ và tên", tenant.FullName ?? tenant.UserName ?? "—");
            InfoRow(col, "Email",     tenant.Email ?? "—");
            InfoRow(col, "Số điện thoại", tenant.PhoneNumber ?? "—");
            col.Item().Height(8);

            // Tài sản thuê
            SectionTitle(col, "ĐIỀU 3 – TÀI SẢN THUÊ");
            InfoRow(col, "Phòng số",  room.RoomNumber);
            InfoRow(col, "Tòa nhà",   bldg.Name);
            InfoRow(col, "Địa chỉ",   bldg.Address + ", " + bldg.Ward + ", " + bldg.District + ", " + bldg.Province);
            InfoRow(col, "Diện tích", $"{room.Area:N1} m²");
            InfoRow(col, "Sức chứa",  room.Capacity.HasValue ? $"{room.Capacity} người" : "Không giới hạn");
            col.Item().Height(8);

            // Thời hạn & tài chính
            SectionTitle(col, "ĐIỀU 4 – THỜI HẠN VÀ GIÁ THUÊ");
            InfoRow(col, "Ngày bắt đầu",  contract.StartDate.ToString("dd/MM/yyyy"));
            InfoRow(col, "Ngày kết thúc", contract.EndDate.HasValue
                ? contract.EndDate.Value.ToString("dd/MM/yyyy") : "Không xác định");
            InfoRow(col, "Giá thuê/tháng", $"{contract.MonthlyRent:N0} đồng");
            InfoRow(col, "Tiền cọc",       $"{contract.DepositAmount:N0} đồng");
            col.Item().Height(8);

            // Điều khoản
            SectionTitle(col, "ĐIỀU 5 – ĐIỀU KHOẢN CHUNG");
            Clause(col, "1.", "Bên B có trách nhiệm thanh toán tiền thuê đúng hạn vào đầu mỗi tháng.");
            Clause(col, "2.", "Bên B không được tự ý sửa chữa, cải tạo phòng khi chưa có sự đồng ý của Bên A.");
            Clause(col, "3.", "Bên B có trách nhiệm giữ gìn vệ sinh chung và tuân thủ nội quy tòa nhà.");
            Clause(col, "4.", "Khi chấm dứt hợp đồng, Bên B phải thông báo trước ít nhất 30 ngày.");
            Clause(col, "5.", "Tiền cọc sẽ được hoàn trả sau khi Bên B bàn giao phòng và thanh toán đầy đủ các khoản phí.");
            col.Item().Height(8);

            if (!string.IsNullOrWhiteSpace(contract.Notes))
            {
                SectionTitle(col, "ĐIỀU 6 – GHI CHÚ THÊM");
                col.Item().Text(contract.Notes);
                col.Item().Height(8);
            }

            // Ký tên
            col.Item().Height(20);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().AlignCenter().Text("BÊN A").Bold();
                    sig.Item().AlignCenter().Text("(Chủ trọ)").FontSize(9).FontColor(Colors.Grey.Medium);
                    sig.Item().Height(50);
                    sig.Item().AlignCenter().Text(bldg.Landlord?.FullName ?? bldg.Name).FontSize(9);
                });
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().AlignCenter().Text("BÊN B").Bold();
                    sig.Item().AlignCenter().Text("(Người thuê)").FontSize(9).FontColor(Colors.Grey.Medium);
                    sig.Item().Height(50);
                    sig.Item().AlignCenter().Text(tenant.FullName ?? "—").FontSize(9);
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
