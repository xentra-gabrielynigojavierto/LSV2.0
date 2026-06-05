using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Liens.Infrastructure.Documents;

public sealed class BillOfSalePdfGenerator : IBillOfSalePdfGenerator
{
    public byte[] Generate(BillOfSale bos)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("BILL OF SALE").Bold().FontSize(18).FontColor(Colors.Grey.Darken3);
                    col.Item().PaddingTop(4).Text(bos.BillOfSaleNumber).FontSize(12).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(2).Text($"Status: {bos.Status}").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(6);

                    SectionHeading(col, "Transaction Details");
                    InfoRow(col, "Lien ID", bos.LienId.ToString());
                    InfoRow(col, "Lien Offer ID", bos.LienOfferId.ToString());
                    if (!string.IsNullOrWhiteSpace(bos.ExternalReference))
                        InfoRow(col, "External Reference", bos.ExternalReference);

                    col.Item().PaddingTop(8);
                    SectionHeading(col, "Parties");
                    InfoRow(col, "Seller Organization", bos.SellerOrgId.ToString());
                    if (!string.IsNullOrWhiteSpace(bos.SellerContactName))
                        InfoRow(col, "Seller Contact", bos.SellerContactName);
                    InfoRow(col, "Buyer Organization", bos.BuyerOrgId.ToString());
                    if (!string.IsNullOrWhiteSpace(bos.BuyerContactName))
                        InfoRow(col, "Buyer Contact", bos.BuyerContactName);

                    col.Item().PaddingTop(8);
                    SectionHeading(col, "Financial Summary");
                    InfoRow(col, "Purchase Amount", $"${bos.PurchaseAmount:N2}");
                    InfoRow(col, "Original Lien Amount", $"${bos.OriginalLienAmount:N2}");
                    if (bos.DiscountPercent.HasValue)
                        InfoRow(col, "Discount", $"{bos.DiscountPercent.Value:N2}%");

                    col.Item().PaddingTop(8);
                    SectionHeading(col, "Dates");
                    InfoRow(col, "Issued", bos.IssuedAtUtc.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                    if (bos.ExecutedAtUtc.HasValue)
                        InfoRow(col, "Executed", bos.ExecutedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                    if (bos.EffectiveAtUtc.HasValue)
                        InfoRow(col, "Effective", bos.EffectiveAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                    if (bos.CancelledAtUtc.HasValue)
                        InfoRow(col, "Cancelled", bos.CancelledAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));

                    if (!string.IsNullOrWhiteSpace(bos.Terms))
                    {
                        col.Item().PaddingTop(8);
                        SectionHeading(col, "Terms");
                        col.Item().PaddingLeft(4).Text(bos.Terms).FontSize(9);
                    }

                    if (!string.IsNullOrWhiteSpace(bos.Notes))
                    {
                        col.Item().PaddingTop(8);
                        SectionHeading(col, "Notes");
                        col.Item().PaddingLeft(4).Text(bos.Notes).FontSize(9);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" — LegalSynq").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void SectionHeading(ColumnDescriptor col, string title)
    {
        col.Item().Text(title).Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
    }

    private static void InfoRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem(1).Text(label).FontColor(Colors.Grey.Darken2);
            row.RelativeItem(2).Text(value);
        });
    }
}
