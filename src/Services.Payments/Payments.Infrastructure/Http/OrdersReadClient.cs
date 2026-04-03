using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Payments.Application.Abstractions;
using Shared.Contracts.Orders;

namespace Payments.Infrastructure.Http;

public sealed class OrdersReadClient(HttpClient http) : IOrdersReadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<OrderResponse?> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        using var res = await http.GetAsync($"orders/{orderId:D}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!res.IsSuccessStatusCode)
            return null;

        return await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOpts, cancellationToken: ct);
    }
}
