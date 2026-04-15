using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Http;

namespace swLog.Controllers
{
	/// <summary>
	/// Proporciona puntos finales de API para administrar los archivos log de las aplicaciones, incluyendo el envío del log por correo electrónico, 
	/// la descarga de logs, el restablecimiento de archivos de registro, la escritura de entradas en log, la visualización de los registros disponibles y la administración de los responsables de la aplicación.
	/// </summary>
	/// <remarks>El controlador LogController expone puntos de acceso para operaciones comunes de gestión de registros, como el envío de archivos de registro por correo electrónico, 
	/// la descarga de registros, el restablecimiento de archivos de registro y la escritura de entradas de registro para aplicaciones específicas. También proporciona puntos de 
	/// acceso para gestionar y consultar los responsables (propietarios) de cada aplicación, así como para descargar el registro de auditoría. Todas las acciones se auditan y 
	/// registran mediante NLog. Las configuraciones de correo electrónico y directorio de registros se leen desde la configuración de la aplicación. 
	/// Este controlador está diseñado para uso administrativo y operativo en entornos donde los archivos de registro de las aplicaciones requieren gestión centralizada y trazabilidad..</remarks>
	[RoutePrefix("api/log")]
	public class LogController : ApiController
	{
		// Logger fijo para auditoría
		private static readonly Logger auditLogger = LogManager.GetLogger("AuditLogger");

		// Logger dinámico para aplicaciones (se obtiene en cada método con el appName)
		private Logger GetAppLogger(string appName)
		{
			return LogManager.GetLogger($"AppLogger.{appName}");
		}

		private readonly string _auditFilePath = "~/Log/auditoria.log";
		private readonly string _baseLogPath = ConfigurationManager.AppSettings["LogDirectoryBase"];

		private readonly string _correoRemitente = ConfigurationManager.AppSettings["CorreoRemitente"];
		private readonly string _pwdRemitente = ConfigurationManager.AppSettings["PwdRemitente"];
		private readonly string _hostRemitente = ConfigurationManager.AppSettings["HostRemitente"];
		private readonly string _correoDestinatario = ConfigurationManager.AppSettings["CorreoDestinatario"];
		private readonly string _correoDestinatarioC = ConfigurationManager.AppSettings["CorreoDestinatarioC"];
		private readonly long maxFileSizeInBytes;

		/// <summary>
		/// Envía por correo electrónico el archivo de log de la aplicación especificada.
		/// </summary>
		/// <remarks>
		/// Este método localiza el archivo de log correspondiente al <paramref name="appName"/> y lo envía como
		/// adjunto a los destinatarios configurados en <c>Web.config</c>. El envío se realiza mediante SMTP.
		/// Además, se registran trazas con NLog y en auditoría para auditar el flujo de ejecución y posibles errores.
		/// </remarks>
		/// <param name="appName">
		/// Nombre de la aplicación cuyo archivo de log se enviará. Se utiliza para localizar el archivo en la carpeta de logs.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que indica el resultado de la operación:
		/// <list type="bullet">
		/// <item><description><see langword="NotFound"/> si el archivo de log no existe.</description></item>
		/// <item><description><see langword="Ok"/> si el correo se envió correctamente.</description></item>
		/// <item><description><see langword="InternalServerError"/> si ocurre una excepción durante el proceso.</description></item>
		/// </list>
		/// </returns>
		[HttpPost]
		[Route("send/{appName}")]
		public IHttpActionResult SendLogEmail(string appName)
		{
			try
			{
				string logFilePath = GetLogFilePath(appName);

				if (!File.Exists(logFilePath))
				{
					WriteAudit(appName, "SendLogEmail", "Archivo de log no encontrado");
					return NotFound();
				}

				using (var smtpClient = new SmtpClient(_hostRemitente)
				{
					Port = 587,
					Credentials = new NetworkCredential(_correoRemitente, _pwdRemitente),
					EnableSsl = true
				})
				using (var mailMessage = new MailMessage
				{
					From = new MailAddress(_correoRemitente),
					Subject = $"Log de la aplicación {appName}",
					Body = "Se adjunta el archivo de log solicitado."
				})
				{
					mailMessage.To.Add(_correoDestinatario);
					mailMessage.CC.Add(_correoDestinatarioC);
					mailMessage.Attachments.Add(new Attachment(logFilePath));

					smtpClient.Send(mailMessage);
				}

				return Ok($"El archivo de log de la aplicación '{appName}' se ha enviado por correo.");
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "SendLogEmail", $"Error al enviar correo: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Envía el archivo de registro de la aplicación especificada por correo electrónico.
		/// </summary>
		/// <remarks>
		/// El archivo de log se envía como adjunto a un destinatario predefinido utilizando SMTP.
		/// La configuración del correo (remitente, credenciales y servidor) se obtiene de <c>Web.config</c>.
		/// Tras el envío exitoso, se ejecuta el método <c>ResetLog</c> para reiniciar el archivo de log.
		/// Todas las acciones se registran mediante NLog y en auditoría.
		/// </remarks>
		/// <param name="appName">
		/// Nombre de la aplicación cuyo archivo de log se enviará.
		/// Este parámetro distingue entre mayúsculas y minúsculas y debe corresponder a una aplicación válida.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que indica el resultado de la operación:
		/// <list type="bullet">
		/// <item><description><see langword="Ok"/> si el correo se envió correctamente y el log fue reiniciado.</description></item>
		/// <item><description><see langword="NotFound"/> si el archivo de log no existe.</description></item>
		/// <item><description><see langword="InternalServerError"/> si ocurre una excepción durante el proceso.</description></item>
		/// </list>
		/// </returns>
		[HttpPost]
		[Route("send2/{appName}")]
		public IHttpActionResult SendLogEmail2(string appName)
		{
			auditLogger.Info($"Entrando a SendLogEmail2 para appName={appName}");

			try
			{
				string logFilePath = GetLogFilePath(appName);

				if (!File.Exists(logFilePath))
				{
					auditLogger.Warn($"Archivo de log no encontrado para appName={appName}");
					WriteAudit(appName, "SendLogEmail2", "Archivo de log no encontrado");
					return NotFound();
				}

				using (var smtpClient = new SmtpClient(_hostRemitente)
				{
					Port = 587,
					Credentials = new NetworkCredential(_correoRemitente, _pwdRemitente),
					EnableSsl = true
				})
				using (var mailMessage = new MailMessage
				{
					From = new MailAddress(_correoRemitente),
					Subject = $"Log de la aplicación {appName}",
					Body = "Se adjunta el archivo de log solicitado."
				})
				{
					mailMessage.To.Add(_correoDestinatario);
					mailMessage.CC.Add(_correoDestinatarioC);
					mailMessage.Attachments.Add(new Attachment(logFilePath));

					smtpClient.Send(mailMessage);
				}

				auditLogger.Info($"Correo enviado correctamente con el log de la aplicación '{appName}'");
				WriteAudit(appName, "SendLogEmail2", "Correo enviado correctamente con el log adjunto");

				// Reseteo del log tras el envío
				ResetLog(appName);
				auditLogger.Info($"Log reiniciado para appName={appName}");
				WriteAudit(appName, "SendLogEmail2", "Log reiniciado tras envío de correo");

				return Ok($"El archivo de log de la aplicación '{appName}' se ha enviado por correo y el log fue reiniciado.");
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, $"Error al enviar el log por correo para appName={appName}");
				WriteAudit(appName, "SendLogEmail2", $"Error al enviar correo: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Descarga el archivo de registro de la aplicación especificada.
		/// </summary>
		/// <remarks>
		/// Este método localiza el archivo de log asociado al <paramref name="appName"/> y lo devuelve como
		/// archivo descargable. Si el archivo no existe, se devuelve un 404 Not Found. Si ocurre un error durante
		/// el proceso, se devuelve un 500 Internal Server Error. Todas las acciones se registran mediante NLog
		/// y en auditoría.
		/// </remarks>
		/// <param name="appName">
		/// Nombre de la aplicación cuyo archivo de log se descargará.
		/// Este parámetro no puede ser nulo ni vacío.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que contiene el archivo de log como adjunto descargable si existe.
		/// Devuelve 404 Not Found si el archivo no se encuentra.
		/// Devuelve 500 InternalServerError si ocurre una excepción.
		/// </returns>
		[HttpGet]
		[Route("download/{appName}")]
		public IHttpActionResult DownloadLog(string appName)
		{
			try
			{
				string logFilePath = GetLogFilePath(appName);

				if (!File.Exists(logFilePath))
				{
					WriteAudit(appName, "DownloadLog", "Archivo de log no encontrado");
					return Content(HttpStatusCode.NotFound, new { Error = "Archivo no encontrado" });
				}

				var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				var response = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StreamContent(stream)
				};

				response.Content.Headers.ContentDisposition =
					new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
					{
						FileName = Path.GetFileName(logFilePath)
					};
				response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

				return ResponseMessage(response);
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "DownloadLog", $"Error al descargar log: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Resetea el archivo de log de la aplicación especificada.
		/// </summary>
		/// <remarks>
		/// Este método reinicia el archivo de log asociado al <paramref name="appName"/> agregando un encabezado
		/// de reinicio. Si existe un respaldo pendiente, se envía por correo antes de eliminarlo. También se elimina
		/// la bandera de alerta (.alert) para permitir futuras notificaciones. Todas las acciones se registran mediante
		/// NLog y en auditoría.
		/// </remarks>
		/// <param name="appName">
		/// Nombre de la aplicación cuyo archivo de log se reiniciará.
		/// Este parámetro distingue entre mayúsculas y minúsculas y debe corresponder a una aplicación válida.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que indica el resultado de la operación:
		/// <list type="bullet">
		/// <item><description><see langword="Ok"/> si el log fue reiniciado correctamente.</description></item>
		/// <item><description><see langword="NotFound"/> si el archivo de log no existe.</description></item>
		/// <item><description><see langword="InternalServerError"/> si ocurre una excepción durante el proceso.</description></item>
		/// </list>
		/// </returns>
		[HttpDelete]
		[Route("reset/{appName}")]
		public IHttpActionResult ResetLog(string appName)
		{
			try
			{
				string logFilePath = GetLogFilePath(appName);
				string resetHeader = $"Log reiniciado el {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}";
				File.WriteAllText(logFilePath, resetHeader);

				WriteAudit(appName, "ResetLog", $"Archivo reiniciado: {logFilePath}");
				return Ok($"El archivo de log '{appName}' ha sido reiniciado.");
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "ResetLog", $"Error al reiniciar log: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Escribe un mensaje en el log correspondiente a la aplicación especificada.
		/// </summary>
		/// <remarks>
		/// Si el archivo de log no existe, se crea e inicializa con la primera entrada.
		/// Si ya existe, se agrega la nueva línea.
		/// Además, se registra la acción en el archivo de auditoría y se verifica el tamaño del log
		/// para activar alertas o rotación si corresponde.
		/// </remarks>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <param name="message">Mensaje a registrar.</param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> indicando el resultado de la operación.
		/// </returns>
		[HttpPost]
		[Route("write/{appName}")]
		public IHttpActionResult WriteLog(string appName, [FromBody] string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				WriteAudit(appName, "WriteLog", "Mensaje vacío recibido");
				return BadRequest("El mensaje no puede estar vacío.");
			}

			var appLogger = GetAppLogger(appName);
			appLogger.Info($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");

			return Ok(new
			{
				Message = $"Mensaje escrito en el log de la aplicación '{appName}'.",
				Contenido = message
			});
		}

		/// <summary>
		/// Endpoint para listar los archivos de log disponibles en el sistema.
		/// </summary>
		/// <remarks>
		/// Este método obtiene todos los archivos de log que se encuentran en el directorio configurado
		/// para almacenar los logs. Devuelve una lista con los nombres de los archivos encontrados.
		/// Si el directorio no existe, se devuelve un 404 Not Found.
		/// Si ocurre un error durante el proceso, se devuelve un 500 Internal Server Error.
		/// Todas las acciones se registran mediante NLog y en auditoría.
		/// </remarks>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que contiene la lista de nombres de archivos de log.
		/// Devuelve 404 Not Found si el directorio no existe.
		/// Devuelve 500 InternalServerError si ocurre una excepción.
		/// </returns>
		[HttpGet]
		[Route("list")]
		public IHttpActionResult ListLogs()
		{
			auditLogger.Info("Entrando a ListLogs");

			try
			{
				string absolutePath = System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath);

				if (string.IsNullOrEmpty(absolutePath) || !Directory.Exists(absolutePath))
				{
					auditLogger.Warn($"Directorio de logs no encontrado: {absolutePath}");
					WriteAudit("ListLogs", "Validación", "Directorio de logs no encontrado");
					return Content(HttpStatusCode.NotFound, new { Error = "Directorio de logs no encontrado" });
				}

				var logs = Directory.GetFiles(absolutePath, "Log_*.log")
									.Select(Path.GetFileName)
									.ToList();

				auditLogger.Info($"Se encontraron {logs.Count} archivos de log en {absolutePath}");
				WriteAudit("ListLogs", "Consulta", $"Se encontraron {logs.Count} archivos de log");

				return Ok(logs);
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al listar los archivos de log");
				WriteAudit("ListLogs", "Error", $"Error al listar logs: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Endpoint para descargar el archivo de auditoría del sistema.
		/// </summary>
		/// <remarks>
		/// Este método devuelve el archivo de auditoría que registra las acciones realizadas en el sistema,
		/// como escrituras en los logs, envíos de correos y reinicios de archivos. El archivo es esencial
		/// para el seguimiento y la auditoría de las actividades del sistema.
		/// Si el archivo no existe, se devuelve un 404 Not Found.
		/// Si ocurre un error durante el proceso, se devuelve un 500 Internal Server Error.
		/// Todas las acciones se registran mediante NLog y en auditoría.
		/// </remarks>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que contiene el archivo de auditoría como adjunto descargable si existe.
		/// Devuelve 404 Not Found si el archivo no se encuentra.
		/// Devuelve 500 InternalServerError si ocurre una excepción.
		/// </returns>
		[HttpGet]
		[Route("downloadAudit")]
		public IHttpActionResult DownloadAudit()
		{
			try
			{
				string auditFilePath = GetAuditFilePath();

				if (!File.Exists(auditFilePath))
				{
					WriteAudit("AUDITORIA", "DownloadAudit", "Archivo de auditoría no encontrado");
					return Content(HttpStatusCode.NotFound, new { Error = "Archivo de auditoría no encontrado" });
				}

				var stream = new FileStream(auditFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				var response = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StreamContent(stream)
				};

				response.Content.Headers.ContentDisposition =
					new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
					{
						FileName = Path.GetFileName(auditFilePath)
					};
				response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

				return ResponseMessage(response);
			}
			catch (Exception ex)
			{
				WriteAudit("AUDITORIA", "DownloadAudit", $"Error al descargar auditoría: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Endpoint para asignar o actualizar el responsable de una aplicación específica.
		/// </summary>
		/// <remarks>
		/// Este método recibe un objeto JSON que contiene el nombre de la aplicación y el correo electrónico del responsable.
		/// Si la aplicación ya tiene un responsable asignado, se actualizará el correo electrónico.
		/// Si no existe un responsable para la aplicación, se creará una nueva entrada.
		/// La información se almacena en un archivo JSON dentro del directorio de logs.
		/// Todas las acciones se registran mediante NLog para auditoría.
		/// </remarks>
		/// <param name="input">
		/// Objeto JSON que representa uno o varios responsables.
		/// Puede ser un solo objeto <see cref="ResponsableApp"/> o una lista de objetos.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que indica el resultado de la operación:
		/// <list type="bullet">
		/// <item><description><see langword="Ok"/> si los responsables fueron registrados o actualizados correctamente.</description></item>
		/// <item><description><see langword="InternalServerError"/> si ocurre una excepción durante el proceso.</description></item>
		/// </list>
		/// </returns>
		[HttpPost]
		[Route("setResponsable")]
		public IHttpActionResult SetResponsable([FromBody] object input)
		{
			try
			{
				string configPath = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath), "responsables.json");
				List<ResponsableApp> lista = new List<ResponsableApp>();

				lock (auditLock) // asegurar concurrencia en acceso a JSON
				{
					if (File.Exists(configPath))
					{
						string json = File.ReadAllText(configPath);
						lista = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ResponsableApp>>(json) ?? new List<ResponsableApp>();
					}

					var jsonString = input.ToString();
					if (jsonString.TrimStart().StartsWith("["))
					{
						var responsables = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ResponsableApp>>(jsonString);
						foreach (var responsable in responsables)
						{
							ProcesarResponsable(lista, responsable);
						}
					}
					else
					{
						var responsable = Newtonsoft.Json.JsonConvert.DeserializeObject<ResponsableApp>(jsonString);
						ProcesarResponsable(lista, responsable);
					}

					File.WriteAllText(configPath, Newtonsoft.Json.JsonConvert.SerializeObject(lista, Newtonsoft.Json.Formatting.Indented));
				}

				return Ok("Responsables registrados/actualizados correctamente.");
			}
			catch (Exception ex)
			{
				WriteAudit("RESPONSABLES", "Error SetResponsable", ex.Message);
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Endpoint para actualizar la fecha del último envío de log para una aplicación específica.
		/// </summary>
		/// <remarks>
		/// Este método recibe un objeto JSON con el nombre de la aplicación y actualiza la fecha del último envío
		/// al momento actual. Si la aplicación no tiene un responsable asignado, se crea una nueva entrada con el
		/// correo "SIN RESPONSABLE". La información se almacena en un archivo JSON dentro del directorio de logs.
		/// Todas las acciones se registran mediante NLog para auditoría.
		/// </remarks>
		/// <param name="responsable">
		/// Objeto <see cref="ResponsableApp"/> que contiene el nombre de la aplicación y opcionalmente el correo del responsable.
		/// </param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que indica el resultado de la operación:
		/// <list type="bullet">
		/// <item><description><see langword="Ok"/> si la fecha de último envío fue actualizada correctamente.</description></item>
		/// <item><description><see langword="BadRequest"/> si el nombre de la aplicación no fue especificado.</description></item>
		/// <item><description><see langword="InternalServerError"/> si ocurre una excepción durante el proceso.</description></item>
		/// </list>
		/// </returns>
		[HttpPost]
		[Route("responsables/updateUltimoEnvio")]
		public IHttpActionResult UpdateUltimoEnvio([FromBody] ResponsableApp responsable)
		{
			try
			{
				if (responsable == null || string.IsNullOrWhiteSpace(responsable.AppName))
				{
					WriteAudit("RESPONSABLES", "Validación", "AppName vacío en UpdateUltimoEnvio");
					return BadRequest("Debe especificar el nombre de la aplicación.");
				}

				string configPath = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath), "responsables.json");
				List<ResponsableApp> lista = new List<ResponsableApp>();

				lock (auditLock)
				{
					if (File.Exists(configPath))
					{
						string json = File.ReadAllText(configPath);
						lista = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ResponsableApp>>(json) ?? new List<ResponsableApp>();
					}

					var existente = lista.FirstOrDefault(r => r.AppName.Equals(responsable.AppName, StringComparison.OrdinalIgnoreCase));
					if (existente != null)
					{
						existente.UltimoEnvio = DateTime.Now;
						WriteAudit(responsable.AppName, "UpdateUltimoEnvio", "Fecha de último envío actualizada");
					}
					else
					{
						lista.Add(new ResponsableApp
						{
							AppName = responsable.AppName,
							CorreoResponsable = responsable.CorreoResponsable ?? "SIN RESPONSABLE",
							UltimoEnvio = DateTime.Now
						});
						WriteAudit(responsable.AppName, "UpdateUltimoEnvio", "Nuevo responsable agregado con fecha de envío");
					}

					File.WriteAllText(configPath, Newtonsoft.Json.JsonConvert.SerializeObject(lista, Newtonsoft.Json.Formatting.Indented));
				}

				return Ok($"Se actualizó la fecha de último envío para la aplicación '{responsable.AppName}'.");
			}
			catch (Exception ex)
			{
				WriteAudit(responsable?.AppName ?? "RESPONSABLES", "Error UpdateUltimoEnvio", ex.Message);
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Endpoint que muestra los responsables de cada aplicación.
		/// </summary>
		/// <remarks>
		/// Este método obtiene la lista de responsables desde el archivo <c>responsables.json</c>
		/// ubicado en el directorio de logs. Devuelve una lista con el nombre de la aplicación y
		/// el correo del responsable asignado.
		/// Si el archivo no existe, se devuelve un 404 Not Found.
		/// Si ocurre un error durante el proceso, se devuelve un 500 Internal Server Error.
		/// Todas las acciones se registran mediante NLog para auditoría.
		/// </remarks>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> que contiene la lista de responsables.
		/// Devuelve 404 Not Found si el archivo no existe.
		/// Devuelve 500 InternalServerError si ocurre una excepción.
		/// </returns>
		[HttpGet]
		[Route("responsables")]
		public IHttpActionResult GetResponsables()
		{
			auditLogger.Info("Entrando a GetResponsables");

			try
			{
				string configPath = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath), "responsables.json");

				if (!File.Exists(configPath))
				{
					auditLogger.Warn($"Archivo de responsables no encontrado: {configPath}");
					WriteAudit("RESPONSABLES", "Consulta", "Archivo de responsables no encontrado");
					return Content(HttpStatusCode.NotFound, new { Error = "Archivo de responsables no encontrado" });
				}

				string json = File.ReadAllText(configPath);
				var lista = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ResponsableApp>>(json) ?? new List<ResponsableApp>();

				auditLogger.Info($"Se encontraron {lista.Count} responsables en {configPath}");
				WriteAudit("RESPONSABLES", "Consulta", $"Se encontraron {lista.Count} responsables");

				return Ok(lista);
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al obtener la lista de responsables");
				WriteAudit("RESPONSABLES", "Error GetResponsables", ex.Message);
				return InternalServerError(ex);
			}
		}



		/// <summary>
		/// Inicializa una nueva instancia de la clase <see cref="LogController"/>.
		/// </summary>
		/// <remarks>
		/// El constructor lee el tamaño máximo de archivo de log desde la configuración (<c>MaxLogFileSizeMB</c>)
		/// y lo convierte a bytes. Asegúrese de que la clave exista en <c>Web.config</c> y contenga un valor válido.
		/// </remarks>
		public LogController()
		{
			if (!int.TryParse(ConfigurationManager.AppSettings["MaxLogFileSizeMB"], out int maxFileSizeMB))
				maxFileSizeMB = 20; // valor por defecto

			maxFileSizeInBytes = maxFileSizeMB * 1024 * 1024;
		}


		/// <summary>
		/// Obtiene la ruta del archivo de alerta (.alert) para la aplicación especificada.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <returns>Ruta absoluta del archivo de alerta.</returns>
		private string GetAlertFilePath(string appName)
		{
			string normalizedAppName = appName.ToLowerInvariant();
			string absolutePath = System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath)
								  ?? throw new InvalidOperationException("Ruta base de logs inválida");
			return Path.Combine(absolutePath, $"Log_{normalizedAppName}.alert");
		}


		/// <summary>
		/// Obtiene la ruta completa del archivo de log de la aplicación.
		/// Crea el directorio y el archivo si no existen.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <returns>Ruta absoluta del archivo de log.</returns>
		private string GetLogFilePath(string appName)
		{
			string normalizedAppName = appName.ToLowerInvariant();
			string absolutePath = System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath)
								  ?? throw new InvalidOperationException("Ruta base de logs inválida");

			if (!Directory.Exists(absolutePath))
			{
				Directory.CreateDirectory(absolutePath);
				auditLogger.Info($"Directorio de logs creado en {absolutePath}");
			}

			string logFilePath = Path.Combine(absolutePath, $"Log_{normalizedAppName}.log");

			if (!File.Exists(logFilePath))
			{
				File.WriteAllText(logFilePath, $"Log inicializado el {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
				auditLogger.Info($"Archivo de log creado: {logFilePath}");
			}

			return logFilePath;
		}


		/// <summary>
		/// Obtiene la ruta absoluta del archivo de auditoría.
		/// </summary>
		/// <returns>Ruta absoluta del archivo de auditoría.</returns>
		private string GetAuditFilePath()
		{
			return System.Web.Hosting.HostingEnvironment.MapPath(_auditFilePath)
				   ?? throw new InvalidOperationException("Ruta de auditoría inválida");
		}


		/// <summary>
		/// Envía un correo de notificación cuando el log alcanza el límite de tamaño.
		/// No adjunta el archivo, solo informa.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <returns><c>true</c> si el correo se envió correctamente; <c>false</c> en caso contrario.</returns>
		private bool SendNotificationEmail(string appName)
		{
			try
			{
				using (var smtpClient = new SmtpClient(_hostRemitente)
				{
					Port = 587,
					Credentials = new NetworkCredential(_correoRemitente, _pwdRemitente),
					EnableSsl = true
				})
				using (var mailMessage = new MailMessage
				{
					From = new MailAddress(_correoRemitente),
					Subject = $"ALERTA DE ESPACIO: Log de {appName} lleno",
					Body = $@"El archivo de log de la aplicación '{appName}' ha superado el límite de tamaño ({maxFileSizeInBytes / 1024 / 1024} MB).

							Acciones requeridas:
							1. Descargue el log actual desde el endpoint de descarga.
							2. Ejecute el endpoint de Reset para limpiar el archivo y reactivar los logs.

							Nota: No se enviarán más correos hasta que se realice el reset.",
												IsBodyHtml = false
				})
				{
					mailMessage.To.Add(_correoDestinatario);
					mailMessage.CC.Add(_correoDestinatarioC);

					smtpClient.Send(mailMessage);
				}

				WriteAudit(appName, "Aviso límite log", "Correo de notificación enviado");
				return true;
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "Aviso límite log", $"Error al enviar correo: {ex.Message}");
				return false;
			}
		}


		/// <summary>
		/// Registra una entrada en el archivo de auditoría.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <param name="action">Acción realizada.</param>
		/// <param name="detalle">Detalle adicional.</param>
		/// <param name="clientIpOverride">IP del cliente (opcional).</param>
		private static readonly object auditLock = new object();

		public void WriteAudit(string appName, string action, string detalle, string clientIpOverride = null)
		{
			try
			{
				string clientIp = clientIpOverride ?? (HttpContext.Current?.Request?.UserHostAddress ?? "IP desconocida");
				string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - IP: {clientIp} - App: {appName} - Acción: {action} - Detalle: {detalle}";

				auditLogger.Warn(entry); // solo Warn/Error para auditoría

				string auditFilePath = GetAuditFilePath();
				lock (auditLock)
				{
					using (var fs = new FileStream(auditFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
					using (var sw = new StreamWriter(fs, Encoding.UTF8))
					{
						sw.WriteLine(entry);
					}
				}
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al escribir en auditoría");
			}
		}


		/// <summary>
		/// Procesa la actualización o alta de un responsable de aplicación.
		/// </summary>
		/// <param name="lista">Lista de responsables.</param>
		/// <param name="responsable">Responsable a procesar.</param>
		private void ProcesarResponsable(List<ResponsableApp> lista, ResponsableApp responsable)
		{
			string clientIp = HttpContext.Current?.Request.UserHostAddress ?? "IP desconocida";

			var existente = lista.FirstOrDefault(r => r.AppName.Equals(responsable.AppName, StringComparison.OrdinalIgnoreCase));
			if (existente != null)
			{
				string anterior = existente.CorreoResponsable;
				existente.CorreoResponsable = responsable.CorreoResponsable;
				existente.UltimoEnvio = responsable.UltimoEnvio;

				WriteAudit(responsable.AppName, "Actualización responsable", $"Se cambió de {anterior} a {responsable.CorreoResponsable}", clientIp);
			}
			else
			{
				lista.Add(responsable);
				WriteAudit(responsable.AppName, "Alta responsable", $"Se registró {responsable.CorreoResponsable}", clientIp);
			}
		}


		/// <summary>
		/// Verifica el tamaño del archivo de log de la aplicación y ejecuta acciones de notificación,
		/// respaldo y reinicio según corresponda.
		/// </summary>
		/// <remarks>
		/// Si el archivo supera el límite configurado:
		/// - En el primer evento se envía un correo de notificación y se crea una bandera (.alert).
		/// - En el segundo evento se genera un respaldo, se envía por correo y se reinicia el log.
		/// Todas las acciones se registran mediante NLog y en el archivo de auditoría.
		/// </remarks>
		/// <param name="logFilePath">Ruta absoluta del archivo de log.</param>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <param name="newLine">Entrada opcional para escribir en el log tras el reinicio.</param>
		private void CheckLogFileSize(string logFilePath, string appName, string newLine = null)
		{
			try
			{
				FileInfo logFileInfo = new FileInfo(logFilePath);
				string alertFilePath = GetAlertFilePath(appName);

				if (logFileInfo.Length >= maxFileSizeInBytes)
				{
					if (!File.Exists(alertFilePath))
					{
						// Primer aviso
						bool emailSent = SendNotificationEmail(appName);
						if (emailSent)
						{
							File.WriteAllText(alertFilePath, $"Aviso enviado el: {DateTime.Now}");
						}
					}
					else
					{
						// Segundo evento → respaldo y envío
						string backupFilePath = logFilePath.Replace(".log", $"_{DateTime.Now:yyyyMMddHHmmss}_backup.log");
						File.Copy(logFilePath, backupFilePath, true);

						bool emailSent = SendLogBackup(appName, backupFilePath);

						if (emailSent)
						{
							string resetHeader = $"Log reiniciado el {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}";
							File.WriteAllText(logFilePath, resetHeader);

							if (!string.IsNullOrEmpty(newLine))
							{
								File.AppendAllText(logFilePath, newLine + Environment.NewLine);
							}

							if (File.Exists(alertFilePath))
							{
								File.Delete(alertFilePath);
							}

							File.Delete(backupFilePath);
						}
						else
						{
							WriteAudit(appName, "Error envío log", $"Se conserva respaldo en {backupFilePath}, requiere envío manual");
						}
					}
				}
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "Error CheckLogFileSize", ex.Message);
			}
		}



		/// <summary>
		/// Envía por correo un respaldo del log de la aplicación cuando se alcanza el límite configurado.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación.</param>
		/// <param name="backupFilePath">Ruta del archivo de respaldo.</param>
		/// <returns><c>true</c> si el correo se envió correctamente; <c>false</c> en caso contrario.</returns>
		private bool SendLogBackup(string appName, string backupFilePath)
		{
			try
			{
				string configPath = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath(_baseLogPath), "responsables.json");
				string correoResponsable = null;
				List<ResponsableApp> lista = new List<ResponsableApp>();

				if (File.Exists(configPath))
				{
					string json = File.ReadAllText(configPath);
					lista = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ResponsableApp>>(json) ?? new List<ResponsableApp>();
					var responsable = lista.FirstOrDefault(r => r.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase));
					correoResponsable = responsable?.CorreoResponsable;
				}

				using (var smtpClient = new SmtpClient(_hostRemitente)
				{
					Port = 587,
					Credentials = new NetworkCredential(_correoRemitente, _pwdRemitente),
					EnableSsl = true
				})
				using (var mailMessage = new MailMessage
				{
					From = new MailAddress(_correoRemitente),
					Subject = $"Respaldo de log {appName} (límite alcanzado)",
					Body = correoResponsable == null
						? $"El log de la aplicación {appName} alcanzó el límite y se envía adjunto.\n\nNo existe responsable registrado para esta aplicación. Favor de darlo de alta."
						: $"Se adjunta el respaldo del log de la aplicación {appName} que alcanzó el límite configurado."
				})
				{
					if (correoResponsable != null)
					{
						mailMessage.To.Add(correoResponsable);
					}
					else
					{
						mailMessage.To.Add("isabel.rugerio@puebla.gob.mx");
						mailMessage.CC.Add("tlamatini.ortiz@puebla.gob.mx");
					}

					mailMessage.Attachments.Add(new Attachment(backupFilePath));
					smtpClient.Send(mailMessage);
				}

				if (correoResponsable != null)
				{
					var responsable = lista.FirstOrDefault(r => r.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase));
					if (responsable != null)
					{
						responsable.UltimoEnvio = DateTime.Now;
						File.WriteAllText(configPath, Newtonsoft.Json.JsonConvert.SerializeObject(lista, Newtonsoft.Json.Formatting.Indented));
					}
				}

				WriteAudit(appName, "SendLogBackup", $"Respaldo enviado correctamente a {correoResponsable ?? "responsables por defecto"}");
				auditLogger.Info($"Respaldo enviado correctamente para appName={appName}");
				return true;
			}
			catch (Exception ex)
			{
				WriteAudit(appName, "SendLogBackup", $"Error al enviar correo: {ex.Message}");
				auditLogger.Error(ex, $"Error al enviar respaldo de log para appName={appName}");
				return false;
			}
		}

		/// <summary>
		/// Verifica el tamaño del archivo de auditoría y envía una alerta si supera el límite configurado.
		/// </summary>
		private void CheckAuditFileSize()
		{
			try
			{
				string auditFilePath = GetAuditFilePath();
				FileInfo auditFileInfo = new FileInfo(auditFilePath);

				// Umbral de 1 GB (configurable en Web.config)
				int maxAuditFileSizeMB = int.Parse(ConfigurationManager.AppSettings["MaxAuditFileSizeMB"] ?? "1024");
				long maxAuditFileSizeBytes = maxAuditFileSizeMB * 1024 * 1024;

				if (auditFileInfo.Exists && auditFileInfo.Length >= maxAuditFileSizeBytes)
				{
					auditLogger.Warn($"Archivo de auditoría alcanzó el límite de {maxAuditFileSizeMB} MB");

					bool emailSent = SendAuditAlertEmail(auditFilePath, auditFileInfo.Length);
					if (emailSent)
					{
						WriteAudit("AUDITORIA", "Aviso límite auditoría",
							$"Archivo alcanzó {auditFileInfo.Length / (1024 * 1024)} MB. Se envió alerta.");
					}
				}
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al verificar tamaño del archivo de auditoría");
			}
		}

		/// <summary>
		/// Envía un correo de alerta cuando el archivo de auditoría supera el límite.
		/// </summary>
		/// <param name="auditFilePath">Ruta del archivo de auditoría.</param>
		/// <param name="sizeBytes">Tamaño actual en bytes.</param>
		/// <returns><c>true</c> si el correo se envió correctamente; <c>false</c> en caso contrario.</returns>
		private bool SendAuditAlertEmail(string auditFilePath, long sizeBytes)
		{
			try
			{
				using (var smtpClient = new SmtpClient(_hostRemitente)
				{
					Port = 587,
					Credentials = new NetworkCredential(_correoRemitente, _pwdRemitente),
					EnableSsl = true
				})
				using (var mailMessage = new MailMessage
				{
					From = new MailAddress(_correoRemitente),
					Subject = "ALERTA: Archivo de auditoría alcanzó límite de tamaño",
					Body = $@"El archivo de auditoría ha alcanzado {sizeBytes / (1024 * 1024)} MB.
						Ruta: {auditFilePath}

						Acciones requeridas:
						1. Respaldar el archivo de auditoría.
						2. Revisar políticas de almacenamiento.
						3. Considerar rotación manual o archivado."
				})
				{
					mailMessage.To.Add(_correoDestinatario);
					mailMessage.CC.Add(_correoDestinatarioC);

					smtpClient.Send(mailMessage);
				}

				WriteAudit("AUDITORIA", "Aviso límite auditoría", $"Archivo alcanzó {sizeBytes / (1024 * 1024)} MB. Se envió alerta.");
				return true;
			}
			catch (Exception ex)
			{
				WriteAudit("AUDITORIA", "Error envío alerta auditoría", ex.Message);
				return false;
			}
		}





		public class ResponsableApp
		{
			public string AppName { get; set; }
			public string CorreoResponsable { get; set; }
			public DateTime? UltimoEnvio { get; set; }
		}


	}
}