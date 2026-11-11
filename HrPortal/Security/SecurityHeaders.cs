using Microsoft.AspNetCore.Http;

namespace HrPortal.Security
{
    /// <summary>
    /// Headers on every response that tell the browser to keep the portal
    /// to itself: no framing by other sites, no guessing file types, no
    /// scripts, styles or images from anywhere but the portal, and no
    /// camera, microphone or location.
    /// </summary>
    public static class SecurityHeaders
    {
        /// <summary>
        /// Everything loads from the portal itself. Inline scripts and
        /// styles are still allowed because pages use inline click and
        /// submit handlers and inline widths for progress bars.
        /// </summary>
        public const string ContentSecurityPolicy =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-src 'self'; " +
            "frame-ancestors 'self'; " +
            "form-action 'self'; " +
            "base-uri 'self'; " +
            "object-src 'none'";

        /// <summary>
        /// The content policy goes only on web pages. Sent with a PDF it can
        /// stop the browser's own viewer from opening it.
        /// </summary>
        public static void Apply(IHeaderDictionary headers, string? contentType)
        {
            if (contentType != null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                headers["Content-Security-Policy"] = ContentSecurityPolicy;

            headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        }

        public static Task AddAsync(HttpContext context, Func<Task> next)
        {
            context.Response.OnStarting(() =>
            {
                Apply(context.Response.Headers, context.Response.ContentType);
                return Task.CompletedTask;
            });

            return next();
        }
    }
}
