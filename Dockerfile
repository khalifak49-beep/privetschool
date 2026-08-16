# =====================================================================
#  نظام إدارة المدارس — صورة الإنتاج
#  البناء متعدّد المراحل: مرحلة بناء بالـSDK ثم صورة تشغيل خفيفة
# =====================================================================

# ---------- مرحلة البناء ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# نسخ ملف المشروع أولاً للاستفادة من ذاكرة طبقات Docker عند تكرار البناء
COPY SchoolSys/SchoolSys.csproj SchoolSys/
RUN dotnet restore SchoolSys/SchoolSys.csproj

# نسخ بقية الملفات ثم النشر
COPY . .
RUN dotnet publish SchoolSys/SchoolSys.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- مرحلة التشغيل ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# خطوط عربية ضرورية لتوليد ملفات PDF عبر QuestPDF،
# و libfontconfig مطلوبة لمحرّك الرسم SkiaSharp
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        fontconfig \
        libfontconfig1 \
        fonts-noto-core \
        fonts-noto-ui-core \
        curl \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# مجلد رفع الملفات (مؤقت داخل الحاوية — يُفقد عند إعادة النشر)
RUN mkdir -p /app/wwwroot/uploads

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production \
    PDF_FONT="Noto Sans Arabic" \
    TZ=Asia/Muscat

# Render يمرّر المنفذ عبر متغير PORT، والتطبيق يقرأه في Program.cs
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT:-8080}/healthz" || exit 1

ENTRYPOINT ["dotnet", "SchoolSys.dll"]
