using Microsoft.AspNetCore.Hosting;
using QRCoder;

namespace AssetMgmt.Infrastructure.Services;

public class QrCodeService : IQrCodeService
{
    private readonly IWebHostEnvironment _env;

    public QrCodeService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> GenerateForAssetAsync(string assetCode, CancellationToken ct = default)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var qrDir = Path.Combine(webRoot, "qr");
        Directory.CreateDirectory(qrDir);

        var fileName = $"{assetCode}.png";
        var absolutePath = Path.Combine(qrDir, fileName);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(assetCode, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        await File.WriteAllBytesAsync(absolutePath, png, ct);

        return $"/qr/{fileName}";
    }
}
