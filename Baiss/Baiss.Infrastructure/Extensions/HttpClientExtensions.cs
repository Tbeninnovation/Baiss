using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Baiss.Infrastructure.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, string requestUri, HttpContent content, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
            {
                Content = content
            };
            return await client.SendAsync(request, cancellationToken);
        }
    }
}
