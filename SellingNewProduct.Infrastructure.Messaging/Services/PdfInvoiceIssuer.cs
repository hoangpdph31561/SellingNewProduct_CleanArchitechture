using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;

namespace SellingNewProduct.Infrastructure.Messaging.Services;

/// <summary>Binds the <c>Gateways:Invoice</c> section: where PDFs are written and the seller header.</summary>
public sealed class InvoiceOptions
{
    public const string SectionName = "Gateways:Invoice";

    /// <summary>Directory PDF invoices are written to (created if missing). Relative paths resolve to the content root.</summary>
    public string OutputDirectory { get; set; } = "invoices";

    public string SellerName { get; set; } = "SellingNewProduct JSC";
    public string SellerTaxCode { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;
}

/// <summary>
/// Production invoice gateway: renders a real PDF invoice with QuestPDF and writes it to disk,
/// returning nothing (the file path is logged). Selected instead of <see cref="LoggingInvoiceIssuer"/>
/// when <c>Gateways:UseReal</c> is true. Runs inside the shared Polly pipeline via its command worker.
/// </summary>
public sealed class PdfInvoiceIssuer : IInvoiceIssuer
{
    private readonly InvoiceOptions myOptions;
    private readonly ILogger<PdfInvoiceIssuer> myLogger;

    static PdfInvoiceIssuer()
    {
        // QuestPDF Community license — free for individuals and small businesses. Set once per process.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfInvoiceIssuer(IOptions<InvoiceOptions> theOptions, ILogger<PdfInvoiceIssuer> theLogger)
    {
        myOptions = theOptions.Value;
        myLogger = theLogger;
    }

    public async Task IssueAsync(InvoiceRequest theRequest, CancellationToken theCancellationToken = default)
    {
        Directory.CreateDirectory(myOptions.OutputDirectory);

        var aInvoiceNumber = $"INV-{theRequest.PaymentId:N}".ToUpperInvariant();
        var aPath = Path.Combine(myOptions.OutputDirectory, $"{aInvoiceNumber}.pdf");

        var aBytes = BuildPdf(theRequest, aInvoiceNumber);
        await File.WriteAllBytesAsync(aPath, aBytes, theCancellationToken);

        myLogger.LogInformation(
            "🧾 PDF invoice {Invoice} for order {OrderId} written to {Path} ({Amount:N0} {Currency}).",
            aInvoiceNumber, theRequest.OrderId, aPath, theRequest.Amount, theRequest.Currency);
    }

    private byte[] BuildPdf(InvoiceRequest theRequest, string theInvoiceNumber)
    {
        return Document.Create(theContainer =>
        {
            theContainer.Page(thePage =>
            {
                thePage.Margin(40);
                thePage.Size(PageSizes.A4);
                thePage.DefaultTextStyle(x => x.FontSize(11));

                thePage.Header().Column(theCol =>
                {
                    theCol.Item().Text(myOptions.SellerName).FontSize(18).Bold();
                    if (!string.IsNullOrWhiteSpace(myOptions.SellerAddress))
                    {
                        theCol.Item().Text(myOptions.SellerAddress).FontColor(Colors.Grey.Darken1);
                    }

                    if (!string.IsNullOrWhiteSpace(myOptions.SellerTaxCode))
                    {
                        theCol.Item().Text($"Tax code: {myOptions.SellerTaxCode}").FontColor(Colors.Grey.Darken1);
                    }

                    theCol.Item().PaddingTop(10).Text("INVOICE").FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                });

                thePage.Content().PaddingVertical(20).Column(theCol =>
                {
                    theCol.Spacing(6);
                    theCol.Item().Row(theRow =>
                    {
                        theRow.RelativeItem().Text($"Invoice no: {theInvoiceNumber}");
                        theRow.RelativeItem().AlignRight().Text($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    });
                    theCol.Item().Text($"Order ref: {theRequest.OrderId}");
                    theCol.Item().Text($"Payment ref: {theRequest.PaymentId}");

                    theCol.Item().PaddingTop(16).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    theCol.Item().PaddingTop(10).Row(theRow =>
                    {
                        theRow.RelativeItem().Text("Amount due").Bold();
                        theRow.RelativeItem().AlignRight().Text($"{theRequest.Amount:N0} {theRequest.Currency}").Bold();
                    });
                });

                thePage.Footer().AlignCenter().Text(theText =>
                {
                    theText.Span("Thank you for your purchase — ").FontColor(Colors.Grey.Darken1);
                    theText.Span(myOptions.SellerName).SemiBold();
                });
            });
        }).GeneratePdf();
    }
}
