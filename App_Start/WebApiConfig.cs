using swLog.Areas.HelpPage;
using swLog.Servicios;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.ExceptionHandling;

namespace swLog
{
	public static class WebApiConfig
	{
		public static void Register(HttpConfiguration config)
		{
			// --- Rate limiting ---
			config.MessageHandlers.Add(new RateLimitHandler());

			// --- Rutas por atributos ---
			config.MapHttpAttributeRoutes();

			// --- Ruta por defecto ---
			config.Routes.MapHttpRoute(
				name: "DefaultApi",
				routeTemplate: "api/{controller}/{id}",
				defaults: new { id = RouteParameter.Optional }
			);

			// --- Validación de Content-Type en POST/PUT ---
			config.Filters.Add(new ValidateJsonContentFilter());

			// --- Manejo global de excepciones ---
			config.Services.Replace(typeof(IExceptionHandler), new GlobalExceptionHandler());

			// --- CORS dinámico ---
			string entorno = System.Configuration.ConfigurationManager.AppSettings["Entorno"];
			string[] originsArray = CorsSettings.GetCorsOrigins(entorno);
			string origins = string.Join(",", originsArray);

			var cors = new EnableCorsAttribute(
				origins,
				"Content-Type,Authorization,Token,Accept,Origin",   // headers permitidos
				"GET,POST,PUT,DELETE,OPTIONS"                      // métodos permitidos
			);
			config.EnableCors(cors);

			// --- Documentación XML ---
			var xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + @"bin\swLog.xml";
			if (System.IO.File.Exists(xmlPath))
			{
				config.SetDocumentationProvider(new XmlDocumentationProvider(xmlPath));
			}
		}
	}
}
