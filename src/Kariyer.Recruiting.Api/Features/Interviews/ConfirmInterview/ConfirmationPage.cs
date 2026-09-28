using System.Globalization;
using System.Net;
using System.Text;
using Kariyer.Recruiting.Domain.Interviews;

namespace Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;

/// <summary>
/// The page the candidate lands on from the invitation e-mail. Rendered here rather than in a SPA
/// because the link has to work in any mail client, with no session and no JavaScript.
/// </summary>
public static class ConfirmationPage
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static int StatusOf(ConfirmationOutcome outcome) => outcome switch
    {
        ConfirmationOutcome.Invalid => StatusCodes.Status403Forbidden,
        ConfirmationOutcome.Expired => StatusCodes.Status410Gone,
        ConfirmationOutcome.NotFound => StatusCodes.Status404NotFound,
        ConfirmationOutcome.Closed => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status200OK,
    };

    public static IResult Render(ConfirmationView view)
    {
        bool accepting = view.Answer == ConfirmationAnswer.Accept;

        (string title, string message) = view.Outcome switch
        {
            ConfirmationOutcome.Answerable when accepting =>
                ("Mülakat davetiniz", "Katılacağınızı bildirmek için aşağıdaki düğmeye dokunun."),
            ConfirmationOutcome.Answerable =>
                ("Mülakat davetiniz", "Katılamayacağınızı bildirmek için aşağıdaki düğmeye dokunun."),
            ConfirmationOutcome.Answered when accepting =>
                ("Katılımınız onaylandı", "Görüşmeyi yapacak ekip bilgilendirildi. Görüşmede buluşmak üzere."),
            ConfirmationOutcome.Answered =>
                ("Yanıtınız iletildi", "Katılamayacağınızı bildirdiniz. İlgili ekip bilgilendirildi."),
            ConfirmationOutcome.AlreadyAnswered when accepting =>
                ("Katılımınız zaten onaylı", "Bu daveti daha önce kabul etmiştiniz; yeniden yapmanız gerekmiyor."),
            ConfirmationOutcome.AlreadyAnswered =>
                ("Yanıtınız zaten kayıtlı", "Katılamayacağınızı daha önce bildirmiştiniz."),
            ConfirmationOutcome.Expired =>
                ("Bağlantının süresi dolmuş", "Bu davet bağlantısı artık geçerli değil. Sizinle iletişime geçen ekibe yazabilirsiniz."),
            ConfirmationOutcome.NotFound =>
                ("Mülakat bulunamadı", "Bu davet kaldırılmış olabilir."),
            ConfirmationOutcome.Closed =>
                ("Bu görüşme artık güncellenemiyor", "Görüşme iptal edilmiş ya da zamanı geçmiş olabilir."),
            _ => ("Bağlantı geçersiz", "Bağlantı eksik veya hatalı görünüyor. Lütfen e-postadaki bağlantıyı yeniden deneyin."),
        };

        StringBuilder html = new();

        html.Append(
            """
            <!doctype html>
            <html lang="tr">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
            <meta name="robots" content="noindex">
            <title>Mülakat daveti · Kariyer Zamanı</title>
            <style>
            :root { color-scheme: light; --fg: hsl(222.2 84% 4.9%); --muted: hsl(215.4 16.3% 46.9%);
              --bg: hsl(210 40% 98%); --card: hsl(0 0% 100%); --line: hsl(214.3 31.8% 91.4%);
              --primary: hsl(207 85% 32%); --primary-hover: hsl(207 85% 25%); --secondary: hsl(210 40% 96.1%); }
            * { box-sizing: border-box; }
            body { margin: 0; padding: 2.5rem 1.25rem; background: var(--bg); color: var(--fg);
              font: 400 16px/1.6 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
            main { max-width: 34rem; margin: 0 auto; background: var(--card); border: 1px solid var(--line);
              border-radius: 1rem; padding: 2rem; }
            h1 { margin: 0 0 .5rem; font-size: 1.375rem; font-weight: 500; }
            p { margin: 0 0 1.5rem; color: var(--muted); }
            dl { margin: 0 0 1.75rem; display: grid; grid-template-columns: auto 1fr; gap: .625rem 1.25rem;
              padding: 1.125rem 1.25rem; background: var(--secondary); border-radius: 1rem; }
            dt { color: var(--muted); font-size: .9375rem; }
            dd { margin: 0; font-size: .9375rem; }
            button { width: 100%; padding: .875rem 1.25rem; border: 0; border-radius: 9999px; cursor: pointer;
              background: var(--primary); color: #fff; font-size: 1rem; font-weight: 500; font-family: inherit; }
            button:hover { background: var(--primary-hover); }
            footer { max-width: 34rem; margin: 1.25rem auto 0; color: var(--muted); font-size: .8125rem; text-align: center; }
            </style>
            </head>
            <body>
            <main>
            """);

        html.Append("<h1>").Append(Escape(title)).Append("</h1>");
        html.Append("<p>").Append(Escape(message)).Append("</p>");

        if (view.StartsAt is { } startsAt)
        {
            html.Append("<dl>");
            Row(html, "İlan", view.JobTitle);
            Row(html, "Şirket", view.CompanyName);
            Row(html, "Tarih", LocalTime(startsAt, view.TimeZone));
            Row(html, "Süre", view.DurationMinutes is { } minutes ? $"{minutes} dakika" : null);
            Row(html, "Görüşme şekli", TypeLabel(view.Type));
            html.Append("</dl>");
        }

        if (view.Outcome == ConfirmationOutcome.Answerable)
        {
            html.Append("<form method=\"post\">")
                .Append("<input type=\"hidden\" name=\"token\" value=\"").Append(Escape(view.Token)).Append("\">")
                .Append("<button type=\"submit\">")
                .Append(accepting ? "Daveti kabul ediyorum" : "Katılamayacağım")
                .Append("</button></form>");
        }

        html.Append("</main><footer>Kariyer Zamanı</footer></body></html>");

        return Results.Content(html.ToString(), "text/html; charset=utf-8", Encoding.UTF8, StatusOf(view.Outcome));
    }

    private static void Row(StringBuilder html, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        html.Append("<dt>").Append(Escape(label)).Append("</dt><dd>").Append(Escape(value)).Append("</dd>");
    }

    private static string LocalTime(DateTimeOffset startsAt, string? timeZone)
    {
        TimeZoneInfo zone = ResolveZone(timeZone);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(startsAt, zone);

        return $"{local.ToString("d MMMM yyyy, dddd · HH:mm", Turkish)} ({zone.Id})";
    }

    private static TimeZoneInfo ResolveZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string? TypeLabel(string? type) => type switch
    {
        InterviewType.Video => "Video görüşme",
        InterviewType.InPerson => "Yüz yüze",
        InterviewType.Phone => "Telefon",
        _ => null,
    };

    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
