using Microsoft.AspNetCore.Http;

namespace Payments.Infrastructure.Http;

public sealed class ForwardAuthDelegatingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth))
            request.Headers.TryAddWithoutValidation("Authorization", auth);

        return base.SendAsync(request, cancellationToken);
    }
}
