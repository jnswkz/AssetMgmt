namespace AssetMgmt.Infrastructure.Services;

public interface IQrCodeService
{
    /// <summary>
    /// Generates a QR code PNG for the given asset code, writes it under wwwroot/qr,
    /// and returns the web-relative path (e.g. "/qr/IT-LT-0001.png").
    /// </summary>
    Task<string> GenerateForAssetAsync(string assetCode, CancellationToken ct = default);
}
