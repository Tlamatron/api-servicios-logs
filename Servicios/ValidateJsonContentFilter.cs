using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using System.Web.Http.Controllers;

namespace swLog.Servicios
{
	public class ValidateJsonContentFilter : ActionFilterAttribute
	{
		public override void OnActionExecuting(HttpActionContext actionContext)
		{
			// Solo validar si el método es POST o PUT
			var method = actionContext.Request.Method;
			if (method == HttpMethod.Post || method == HttpMethod.Put)
			{
				var contentType = actionContext.Request.Content.Headers.ContentType?.MediaType;
				if (contentType != "application/json")
				{
					actionContext.Response = actionContext.Request.CreateResponse(
						HttpStatusCode.UnsupportedMediaType,
						"El Content-Type debe ser application/json"
					);
				}
			}
		}
	}
}