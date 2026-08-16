namespace SchoolSys.Helpers;

/// <summary>تفقيط المبالغ إلى كلمات عربية لاستخدامها في سندات القبض.</summary>
public static class NumberToArabicWords
{
    private static readonly string[] Ones =
    [
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
        "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
        "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    ];

    private static readonly string[] Tens =
    [
        "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
    ];

    private static readonly string[] Hundreds =
    [
        "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة",
        "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
    ];

    public static string Convert(decimal amount, string currency = "ر.ع")
    {
        if (amount < 0) return "مبلغ غير صالح";

        var whole = (long)Math.Truncate(amount);
        var fraction = (int)Math.Round((amount - whole) * 1000m, 0);   // ثلاث خانات (بيسة)

        var text = whole == 0 ? "صفر" : ConvertGroup(whole);
        var result = $"{text} {currency}";

        if (fraction > 0)
            result += $" و{ConvertGroup(fraction)} بيسة";

        return "فقط " + result + " لا غير";
    }

    private static string ConvertGroup(long number)
    {
        if (number == 0) return "";

        var parts = new List<string>();

        var millions = number / 1_000_000;
        var thousands = number % 1_000_000 / 1000;
        var rest = number % 1000;

        if (millions > 0) parts.Add(Scale(millions, "مليون", "مليونان", "ملايين"));
        if (thousands > 0) parts.Add(Scale(thousands, "ألف", "ألفان", "آلاف"));
        if (rest > 0) parts.Add(UnderThousand((int)rest));

        return string.Join(" و", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static string Scale(long value, string singular, string dual, string plural)
    {
        return value switch
        {
            1 => singular,
            2 => dual,
            >= 3 and <= 10 => $"{UnderThousand((int)value)} {plural}",
            _ => $"{ConvertGroup(value)} {singular}"
        };
    }

    private static string UnderThousand(int number)
    {
        if (number == 0) return "";

        var parts = new List<string>();
        var hundreds = number / 100;
        var remainder = number % 100;

        if (hundreds > 0) parts.Add(Hundreds[hundreds]);

        if (remainder > 0)
        {
            if (remainder < 20) parts.Add(Ones[remainder]);
            else
            {
                var unit = remainder % 10;
                var ten = remainder / 10;
                parts.Add(unit > 0 ? $"{Ones[unit]} و{Tens[ten]}" : Tens[ten]);
            }
        }

        return string.Join(" و", parts);
    }
}
