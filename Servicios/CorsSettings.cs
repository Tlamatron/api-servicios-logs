using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Hosting;

namespace swLog.Servicios
{
	public static class CorsSettings
	{
		private static readonly string corsPath = HostingEnvironment.MapPath("~/Config/Cors.config");
		private static readonly Configuration corsConfig;

		static CorsSettings()
		{
			var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = corsPath };
			corsConfig = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
		}

		public static string[] GetCorsOrigins(string entorno)
		{
			string key = entorno == "UrlProduccion" ? "CorsOriginsProduccion" : "CorsOriginsPruebas";
			string value = corsConfig.AppSettings.Settings[key]?.Value ?? string.Empty;
			return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		}

		public static string[] GetAppsPermitidas(string entorno)
		{
			string key = entorno == "UrlProduccion" ? "AppsPermitidasProduccion" : "AppsPermitidasPruebas";
			string value = corsConfig.AppSettings.Settings[key]?.Value ?? string.Empty;
			return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		}
	}
}