using AssetMgmt.Application.Handover;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AssetMgmt.Infrastructure.Documents;

/// <summary>
/// QuestPDF template for the asset handover record (Biên bản bàn giao tài sản).
/// Deliberately simple for the MVP — a titled record with the parties, the
/// asset details, and a signature block.
/// </summary>
public class HandoverPdfDocument : IDocument
{
    private readonly HandoverModel _m;

    public HandoverPdfDocument(HandoverModel model)
    {
        _m = model;
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(11).FontColor(Colors.Grey.Darken4));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(20).Element(ComposeBody);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Trang ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("BIÊN BẢN BÀN GIAO TÀI SẢN")
                .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().AlignCenter().Text($"Số: {_m.DocumentNumber}").FontSize(11).Italic();
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeBody(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            col.Item().Text($"Hôm nay, ngày {_m.HandoverDate:dd/MM/yyyy}, chúng tôi gồm:").LineHeight(1.4f);

            col.Item().Column(parties =>
            {
                parties.Spacing(4);
                parties.Item().Text(t =>
                {
                    t.Span("Bên giao (IT): ").SemiBold();
                    t.Span(_m.ApproverName);
                });
                parties.Item().Text(t =>
                {
                    t.Span("Bên nhận (Nhân viên): ").SemiBold();
                    t.Span($"{_m.EmployeeName} — Mã NV: {_m.EmployeeCode}"
                        + (string.IsNullOrWhiteSpace(_m.EmployeeDepartment) ? "" : $" — {_m.EmployeeDepartment}"));
                });
            });

            col.Item().Text("Tiến hành bàn giao tài sản với thông tin như sau:").LineHeight(1.4f);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(160);
                    c.RelativeColumn();
                });

                void Row(string label, string value)
                {
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                        .Background(Colors.Grey.Lighten4).Padding(6).Text(label).SemiBold();
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                        .Padding(6).Text(value);
                }

                Row("Mã tài sản", _m.AssetCode);
                Row("Model", _m.ModelName);
                Row("Số serial", _m.Serial);
                Row("Vị trí", string.IsNullOrWhiteSpace(_m.Location) ? "—" : _m.Location!);
                Row("Nguyên giá", $"{_m.AcquisitionCost:#,##0} VND");
            });

            if (!string.IsNullOrWhiteSpace(_m.Notes))
            {
                col.Item().Text(t =>
                {
                    t.Span("Ghi chú: ").SemiBold();
                    t.Span(_m.Notes!);
                });
            }

            col.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().Text("BÊN GIAO").Bold();
                    c.Item().Text("(Ký, ghi rõ họ tên)").FontSize(9).Italic();
                    c.Item().Height(60);
                    c.Item().Text(_m.ApproverName);
                });
                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().Text("BÊN NHẬN").Bold();
                    c.Item().Text("(Ký, ghi rõ họ tên)").FontSize(9).Italic();
                    c.Item().Height(60);
                    c.Item().Text(_m.EmployeeName);
                });
            });
        });
    }
}
