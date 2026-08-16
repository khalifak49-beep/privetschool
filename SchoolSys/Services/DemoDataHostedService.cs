using SchoolSys.Data;

namespace SchoolSys.Services;

/// <summary>
/// يزرع البيانات التجريبية في الخلفية بعد إقلاع التطبيق.
/// الزرع يتم مرة واحدة فقط (يتوقف إذا وُجد طلاب في القاعدة)،
/// وتشغيله في الخلفية يمنع تأخير فحص الجاهزية على منصات مثل Render.
/// </summary>
public class DemoDataHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<DemoDataHostedService> _logger;

    public DemoDataHostedService(IServiceProvider services, IConfiguration config,
        ILogger<DemoDataHostedService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Seed:DemoData", true))
        {
            _logger.LogInformation("زرع البيانات التجريبية معطّل عبر الإعدادات (Seed:DemoData=false).");
            return;
        }

        // مهلة قصيرة حتى يستقر التطبيق ويستجيب لفحص الجاهزية
        try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        try
        {
            _logger.LogInformation("بدء زرع البيانات التجريبية في الخلفية...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            await DbSeeder.SeedDemoAsync(_services);

            _logger.LogInformation("اكتمل زرع البيانات التجريبية خلال {Seconds} ثانية.",
                sw.Elapsed.TotalSeconds.ToString("0"));
        }
        catch (Exception ex)
        {
            // فشل الزرع لا يجب أن يُسقط التطبيق — النظام يعمل ببيانات فارغة
            _logger.LogError(ex, "فشل زرع البيانات التجريبية. النظام يعمل لكن بلا بيانات تجريبية.");
        }
    }
}
