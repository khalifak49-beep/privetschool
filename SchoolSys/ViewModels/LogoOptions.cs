namespace SchoolSys.ViewModels;

/// <summary>
/// خيارات عرض شعار المدرسة في الجزئية المشتركة "_SchoolLogo".
/// الشعار يظهر في القائمة الجانبية والترويسة وشاشة الدخول والمستندات
/// المطبوعة، ولكل موضع مقاسه، فتُمرَّر الفروق من هنا بدل تكرار المعلّم.
/// </summary>
/// <param name="Size">طول ضلع المربع بأي وحدة CSS.</param>
/// <param name="Radius">استدارة الأركان.</param>
/// <param name="Plain">
/// بلا خلفية ملوّنة — للمستندات المطبوعة حيث الورق أبيض
/// وحبر الخلفيات مُهدَر.
/// </param>
/// <param name="Extra">أصناف CSS إضافية.</param>
public record LogoOptions(
    string Size = "44px",
    string Radius = "13px",
    bool Plain = false,
    string Extra = "");
