using System.Net;
using System.Text;

namespace HrPortal.Services.Notifications
{
    /// <summary>A notification laid out as an email.</summary>
    public sealed record RenderedEmail(string Subject, string Html, string Text);

    /// <summary>
    /// Lays a notification out the way the portal looks: the green
    /// banner with the mark reversed out of it and a gold rule under
    /// it, then a white card with a label saying what kind of message
    /// this is, the message itself and one button back into the
    /// portal.
    ///
    /// Built from tables with the styles written onto each element,
    /// because that is what Outlook and Gmail draw faithfully. The
    /// font is the portal's own where the mail program will load it,
    /// and the nearest system face where it will not.
    /// </summary>
    public static class NotificationEmail
    {
        /// <summary>
        /// The name the embedded logo is referred to by from the
        /// message body.
        /// </summary>
        public const string LogoContentId = "intellego-mark";

        private const string Font =
            "Montserrat,'Segoe UI',Helvetica,Arial,sans-serif";

        private const string Banner = "#0c4a40";
        private const string Gold = "#c2b480";
        private const string Paper = "#eceae5";
        private const string Heading = "#1e293b";
        private const string Ink = "#3f4245";
        private const string Muted = "#6b6f74";
        private const string Primary = "#10675d";

        private sealed record Look(
            string Label,
            string LabelInk,
            string LabelTint,
            string Button);

        private static Look LookOf(EmailKind kind) => kind switch
        {
            EmailKind.ActionNeeded =>
                new("Action needed", "#7e6e1e", "#f6f1dc", "Review the request"),
            EmailKind.Approved =>
                new("Approved", "#157a55", "#e6f5e9", "See the decision"),
            EmailKind.Declined =>
                new("Not approved", "#c53030", "#fdeded", "See the decision"),
            EmailKind.Returned =>
                new("Returned to you", "#9a5b13", "#fff5e6", "Make the changes"),
            EmailKind.TrainingScheduled =>
                new("Training scheduled", "#016a6a", "#e2efed", "View my training"),
            EmailKind.TrainerAssigned =>
                new("You are the trainer", "#016a6a", "#e2efed", "Post the content"),
            EmailKind.ContentDue =>
                new("Content due", "#7e6e1e", "#f6f1dc", "Post the content"),
            EmailKind.ContentOverdue =>
                new("Content overdue", "#c53030", "#fdeded", "Post the content"),
            EmailKind.SessionReminder =>
                new("Coming up", "#016a6a", "#e2efed", "View the training"),
            _ =>
                new("Notification", Primary, "#e2efed", "Open My Intellego")
        };

        public static RenderedEmail Render(
            OutboundNotification notification,
            string? portalBaseUrl,
            bool hasLogo)
        {
            var look = LookOf(notification.Kind);

            var url = AbsoluteLink(portalBaseUrl, notification.Link);

            var greeting = string.IsNullOrWhiteSpace(notification.FirstName)
                ? "Hello,"
                : $"Hi {notification.FirstName.Trim()},";

            return new RenderedEmail(
                notification.Title,
                Html(notification, look, greeting, url, portalBaseUrl, hasLogo),
                Text(notification, look, greeting, url));
        }

        private static string Html(
            OutboundNotification n,
            Look look,
            string greeting,
            string? url,
            string? portalBaseUrl,
            bool hasLogo)
        {
            static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

            var fontFace = string.IsNullOrWhiteSpace(portalBaseUrl)
                ? string.Empty
                : "@font-face{font-family:'Montserrat';font-style:normal;" +
                  "font-weight:300 800;src:url('" +
                  E(portalBaseUrl.TrimEnd('/')) +
                  "/fonts/montserrat/montserrat-latin-variable.woff2') " +
                  "format('woff2');}";

            var mark = hasLogo
                ? $"<img src=\"cid:{LogoContentId}\" width=\"160\" height=\"80\" " +
                  "alt=\"Intellego Investment Consultants\" " +
                  "style=\"display:block;border:0;outline:none;width:160px;height:80px;\">"
                : $"<span style=\"font-family:{Font};font-size:20px;font-weight:700;" +
                  "color:#ffffff;\">Intellego</span>";

            var action = url == null
                ? $"<p style=\"margin:28px 0 0;font-family:{Font};font-size:14px;" +
                  $"line-height:22px;color:{Muted};\">Sign in to My Intellego to see it.</p>"
                : $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" " +
                  "border=\"0\" style=\"margin:28px 0 0;\"><tr>" +
                  $"<td bgcolor=\"{Primary}\" style=\"border-radius:999px;background:{Primary};\">" +
                  $"<a href=\"{E(url)}\" target=\"_blank\" style=\"display:inline-block;" +
                  $"padding:13px 28px;font-family:{Font};font-size:15px;font-weight:700;" +
                  "color:#ffffff;text-decoration:none;border-radius:999px;\">" +
                  $"{E(look.Button)}&nbsp;&rarr;</a></td></tr></table>" +
                  $"<p style=\"margin:20px 0 0;font-family:{Font};font-size:12px;" +
                  $"line-height:18px;color:{Muted};\">Or open this link: " +
                  $"<a href=\"{E(url)}\" style=\"color:{Primary};word-break:break-all;\">" +
                  $"{E(url)}</a></p>";

            var html = new StringBuilder();

            html.Append("<!DOCTYPE html><html lang=\"en\"><head>");
            html.Append("<meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            html.Append("<meta name=\"color-scheme\" content=\"light\">");
            html.Append("<meta name=\"supported-color-schemes\" content=\"light\">");
            html.Append($"<title>{E(n.Title)}</title>");
            html.Append($"<style>{fontFace}body{{margin:0;padding:0;}}" +
                        "a{color:inherit;}</style>");
            html.Append("</head>");

            html.Append($"<body style=\"margin:0;padding:0;background:{Paper};\">");

            html.Append("<div style=\"display:none;max-height:0;overflow:hidden;" +
                        $"mso-hide:all;\">{E(n.Message)}</div>");

            html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" " +
                        $"cellspacing=\"0\" border=\"0\" bgcolor=\"{Paper}\" " +
                        $"style=\"background:{Paper};\"><tr>" +
                        "<td align=\"center\" style=\"padding:32px 16px;\">");

            html.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" " +
                        "cellspacing=\"0\" border=\"0\" " +
                        "style=\"width:100%;max-width:600px;\">");

            html.Append($"<tr><td bgcolor=\"{Banner}\" style=\"background:{Banner};" +
                        "border-radius:14px 14px 0 0;padding:20px 32px;\">" +
                        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" " +
                        "cellspacing=\"0\" border=\"0\"><tr>" +
                        $"<td valign=\"middle\">{mark}</td>" +
                        "<td valign=\"middle\" align=\"right\" " +
                        $"style=\"font-family:{Font};text-align:right;\">" +
                        "<div style=\"font-size:16px;font-weight:700;color:#ffffff;" +
                        "line-height:20px;\">My Intellego</div>" +
                        $"<div style=\"font-size:10px;font-weight:600;color:{Gold};" +
                        "letter-spacing:2px;line-height:16px;\">PEOPLE PORTAL</div>" +
                        "</td></tr></table></td></tr>");

            html.Append($"<tr><td bgcolor=\"{Gold}\" style=\"background:{Gold};" +
                        "height:4px;line-height:4px;font-size:0;\">&nbsp;</td></tr>");

            html.Append("<tr><td bgcolor=\"#ffffff\" style=\"background:#ffffff;" +
                        "border-radius:0 0 14px 14px;padding:36px 32px 34px;\">");

            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" " +
                        "border=\"0\"><tr>" +
                        $"<td bgcolor=\"{look.LabelTint}\" style=\"background:{look.LabelTint};" +
                        "border-radius:999px;padding:6px 14px;" +
                        $"font-family:{Font};font-size:11px;font-weight:700;" +
                        $"letter-spacing:1.5px;color:{look.LabelInk};\">" +
                        $"{E(look.Label.ToUpperInvariant())}</td></tr></table>");

            html.Append($"<h1 style=\"margin:18px 0 0;font-family:{Font};font-size:24px;" +
                        $"line-height:31px;font-weight:700;color:{Heading};\">" +
                        $"{E(n.Title)}</h1>");

            html.Append($"<p style=\"margin:22px 0 0;font-family:{Font};font-size:15px;" +
                        $"line-height:24px;color:{Ink};\">{E(greeting)}</p>");

            html.Append($"<p style=\"margin:10px 0 0;font-family:{Font};font-size:15px;" +
                        $"line-height:24px;color:{Ink};\">{E(n.Message)}</p>");

            html.Append(action);

            html.Append("</td></tr>");

            html.Append("<tr><td align=\"center\" style=\"padding:24px 8px 8px;" +
                        $"font-family:{Font};font-size:12px;line-height:19px;" +
                        $"color:{Muted};text-align:center;\">" +
                        "Intellego Investment Consultants (Pvt) Ltd<br>" +
                        "This is an automatic message from My Intellego. " +
                        "Replies to it are not read." +
                        "</td></tr>");


            html.Append("</table></td></tr></table></body></html>");

            return html.ToString();
        }

        private static string Text(
            OutboundNotification n,
            Look look,
            string greeting,
            string? url)
        {
            var text = new StringBuilder();

            text.AppendLine(look.Label.ToUpperInvariant());
            text.AppendLine(n.Title);
            text.AppendLine();
            text.AppendLine(greeting);
            text.AppendLine();
            text.AppendLine(n.Message.TrimEnd());
            text.AppendLine();

            text.AppendLine(url == null
                ? "Sign in to My Intellego to see it."
                : $"{look.Button}: {url}");

            text.AppendLine();
            text.AppendLine("--");
            text.AppendLine("My Intellego, Intellego Investment Consultants (Pvt) Ltd");
            text.AppendLine("This is an automatic message. Replies to it are not read.");

            return text.ToString();
        }

        private static string? AbsoluteLink(string? portalBaseUrl, string? link)
        {
            if (string.IsNullOrWhiteSpace(link) ||
                string.IsNullOrWhiteSpace(portalBaseUrl))
            {
                return null;
            }

            return portalBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');
        }
    }
}
