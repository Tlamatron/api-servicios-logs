using System.Net;
using System.Net.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Results;

namespace swLog.Servicios
{
	public class GlobalExceptionHandler : ExceptionHandler
	{
		public override void Handle(ExceptionHandlerContext context)
		{
			// Respuesta uniforme en caso de error no controlado
			var response = context.Request.CreateResponse(
				HttpStatusCode.InternalServerError,
				new
				{
					error = true,
					mensaje = "Error interno en el servidor. Contacte al área de soporte."
				}
			);

			context.Result = new ResponseMessageResult(response);
		}
	}
}