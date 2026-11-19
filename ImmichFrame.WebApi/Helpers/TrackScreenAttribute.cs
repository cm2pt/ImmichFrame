using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ImmichFrame.WebApi.Helpers
{
    /// <summary>
    /// Records the calling screen in the <see cref="ScreenRegistry"/> when the request carries a
    /// <c>clientIdentifier</c> query parameter. When an authentication secret is set, only
    /// requests carrying it are recorded: some client endpoints (e.g. Config) are anonymous,
    /// and an unauthenticated caller must not be able to add screens. Requests with a wrong or
    /// missing secret are noted separately, so the admin page can say a screen has a stale one.
    /// </summary>
    public class TrackScreenAttribute : TypeFilterAttribute
    {
        public TrackScreenAttribute() : base(typeof(TrackScreenFilter)) { }

        private sealed class TrackScreenFilter : IAsyncActionFilter
        {
            private readonly ScreenRegistry _registry;
            private readonly IGeneralSettings _settings;

            public TrackScreenFilter(ScreenRegistry registry, IGeneralSettings settings)
            {
                _registry = registry;
                _settings = settings;
            }

            private bool IsAuthenticated(HttpRequest request)
            {
                var secret = _settings.AuthenticationSecret;
                if (string.IsNullOrEmpty(secret))
                {
                    return true;
                }

                var header = request.Headers.Authorization.ToString();
                return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    && header["Bearer ".Length..].Trim() == secret;
            }

            private static bool SentSecret(HttpRequest request)
            {
                var header = request.Headers.Authorization.ToString();
                return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    && header["Bearer ".Length..].Trim().Length > 0;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                var request = context.HttpContext.Request;
                var clientIdentifier = request.Query["clientIdentifier"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(clientIdentifier))
                {
                    var userAgent = request.Headers.UserAgent.FirstOrDefault();
                    var address = context.HttpContext.Connection.RemoteIpAddress?.ToString();
                    if (IsAuthenticated(request))
                    {
                        await _registry.RecordAsync(clientIdentifier, userAgent, address);
                    }
                    else
                    {
                        _registry.RecordRejected(clientIdentifier, userAgent, address, SentSecret(request));
                    }
                }

                await next();
            }
        }
    }
}
