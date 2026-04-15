using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace swLog.Servicios
{
	public class RateLimitHandler : DelegatingHandler
	{
		private static readonly ConcurrentDictionary<string, RequestCounter> _counters
			= new ConcurrentDictionary<string, RequestCounter>();

		private const int LIMIT = 50; // máximo de peticiones
		private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// Usamos el Token como clave
			string clientKey = request.Headers.Contains("Token")
				? request.Headers.GetValues("Token").FirstOrDefault()
				: GetClientIp(request) ?? "anon";// si no envía Token

			var counter = _counters.GetOrAdd(clientKey, _ => new RequestCounter());

			lock (counter)
			{
				if (DateTime.UtcNow - counter.WindowStart >= WINDOW)
				{
					// Reiniciamos la ventana
					counter.Count = 0;
					counter.WindowStart = DateTime.UtcNow;
				}

				counter.Count++;

				if (counter.Count > LIMIT)
				{
					var response = new HttpResponseMessage((HttpStatusCode)429)
					{
						Content = new StringContent($"Cliebte {clientKey} excedió el límite de {LIMIT} peticiones por minuto.")
					};

					// Header estándar para rate limiting
					response.Headers.Add("Retry-After", ((int)WINDOW.TotalSeconds).ToString());

					return response;
				}
			}

			return await base.SendAsync(request, cancellationToken);
		}
		private string GetClientIp(HttpRequestMessage request)
		{
			if (request.Properties.ContainsKey("MS_HttpContext"))
			{
				return ((HttpContextWrapper)request.Properties["MS_HttpContext"]).Request.UserHostAddress;
			}
			return "unknown";
		}
		private class RequestCounter
		{
			public int Count { get; set; } = 0;
			public DateTime WindowStart { get; set; } = DateTime.UtcNow;
		}
	}
}