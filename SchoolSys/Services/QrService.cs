using QRCoder;

namespace SchoolSys.Services;

public interface IQrService
{
    byte[] GeneratePng(string payload, int pixelsPerModule = 10);
    string GenerateDataUrl(string payload, int pixelsPerModule = 6);
}

public class QrService : IQrService
{
    public byte[] GeneratePng(string payload, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public string GenerateDataUrl(string payload, int pixelsPerModule = 6)
        => "data:image/png;base64," + Convert.ToBase64String(GeneratePng(payload, pixelsPerModule));
}
