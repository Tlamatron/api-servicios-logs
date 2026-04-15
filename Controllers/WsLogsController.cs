using Microsoft.IdentityModel.Tokens;
using NLog;
using swLog.Models;
using swLog.Servicios;
using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web.Http;



namespace swLog.Controllers
{
	/// <summary>
	/// Controlador de servicios de seguridad y logging.
	/// </summary>
	/// <remarks>
	/// Este controlador expone endpoints para la creación y validación de tokens.
	/// Se incluyen dos esquemas de seguridad:
	/// 1. Token basado en la clase UserConection.
	/// 2. Token estándar JWT (JSON Web Token).
	/// </remarks>
	[RoutePrefix("api/wsLogs")]
	public class WsLogsController : ApiController
	{
		// Logger exclusivo para auditoría de errores
		private static readonly Logger auditLogger = LogManager.GetLogger("AuditLogger");

		/// <summary>
		/// Genera un token tradicional encriptado para una aplicación autorizada.
		/// </summary>
		/// <remarks>
		/// Recibe un objeto <see cref="UserConection"/> con Usuario, Password y Ambiente.
		/// Devuelve un token encriptado que puede usarse en el encabezado "Token".
		/// Ejemplo:
		/// POST api/wsLogs/crearToken
		/// Body: { "User": "usuario1", "Password": "pwd123", "Ambiente": "1" }
		/// Respuesta: { "Token": "abc123..." }
		/// </remarks>
		/// <param name="user">Objeto <see cref="UserConection"/> con credenciales y nombre de aplicación.</param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el token generado o un mensaje de error si la aplicación no está autorizada.
		/// </returns>
		[HttpPost]
		[Route("crearToken")]
		public IHttpActionResult CrearToken([FromBody] UserConection user)
		{
			auditLogger.Info("Entrando a CrearToken");

			try
			{
				string entorno = ConfigurationManager.AppSettings["Entorno"];
				var apps = CorsSettings.GetAppsPermitidas(entorno).ToList();

				if (string.IsNullOrEmpty(user.nombAplicacion) || !apps.Contains(user.nombAplicacion))
				{
					string detalle = $"Aplicación '{user.nombAplicacion ?? "Desconocida"}' no autorizada para generar tokens en {entorno}";
					auditLogger.Warn(detalle);
					WriteAudit(user.nombAplicacion ?? "Desconocida", "CrearToken", detalle);

					return Content(HttpStatusCode.Forbidden, new
					{
						mensaje = detalle
					});
				}

				string token = Token.CrearToken(user.Motor, user.Ambiente, user.User, user.nombAplicacion, user.TiempoActivo);

				auditLogger.Info($"Token generado correctamente para aplicación '{user.nombAplicacion}'");
				WriteAudit(user.nombAplicacion, "CrearToken", "Token generado correctamente");

				return Ok(new { Token = token });
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, $"Error al generar token para aplicación '{user?.nombAplicacion}'");
				WriteAudit(user?.nombAplicacion ?? "Desconocida", "CrearToken", $"Error: {ex.Message}");
				return InternalServerError(ex);
			}
		}


		/// <summary>
		/// Valida un token tradicional recibido en el encabezado HTTP.
		/// </summary>
		/// <remarks>
		/// El token debe enviarse en el header "Token".
		/// Ejemplo:
		/// POST api/wsLogs/validarToken
		/// Header: Token: abc123...
		/// Respuesta: objeto <see cref="UserConection"/> con estatus y mensajes.
		/// </remarks>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el resultado de la validación.
		/// Devuelve 400 BadRequest si el header no está presente.
		/// Devuelve 500 InternalServerError si ocurre un error durante la validación.
		/// </returns>
		[HttpPost]
		[Route("validarToken")]
		public IHttpActionResult ValidarToken()
		{
			auditLogger.Info("Entrando a ValidarToken");

			try
			{
				var headers = Request.Headers;
				if (!headers.Contains("Token"))
				{
					auditLogger.Warn("Header 'Token' no encontrado en ValidarToken");
					WriteAudit("TOKEN", "ValidarToken", "Header 'Token' no encontrado");
					return BadRequest("El header 'Token' es obligatorio.");
				}

				string token = headers.GetValues("Token").FirstOrDefault();
				if (string.IsNullOrWhiteSpace(token))
				{
					auditLogger.Warn("Header 'Token' vacío en ValidarToken");
					WriteAudit("TOKEN", "ValidarToken", "Header 'Token' vacío");
					return BadRequest("El header 'Token' no puede estar vacío.");
				}

				UserConection userConection = Token.Validar(token);

				// Aquí no se registra en auditoría porque solo los errores van a auditoría
				return Ok(userConection);
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al validar token tradicional");
				WriteAudit("TOKEN", "ValidarToken", $"Error al validar token: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Genera un token JWT firmado para una aplicación autorizada.
		/// </summary>
		/// <remarks>
		/// Recibe un objeto con Usuario, Password, Ambiente y opcionalmente TiempoActivo.
		/// Devuelve un JWT firmado que debe enviarse en el header "Authorization".
		/// Ejemplo:
		/// POST api/wsLogs/generateJwt
		/// Body: { "Usuario": "usuario1", "Password": "pwd123", "Ambiente": 1 }
		/// Respuesta: { "Token": "eyJhbGciOi..." }
		/// </remarks>
		/// <param name="cred">Objeto dinámico con credenciales y nombre de aplicación.</param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el JWT generado o un mensaje de error si la aplicación no está autorizada.
		/// </returns>
		[HttpPost]
		[Route("generateJwt")]
		public IHttpActionResult GenerateJwt([FromBody] dynamic cred)
		{
			auditLogger.Info("Entrando a GenerateJwt");

			try
			{
				string entorno = ConfigurationManager.AppSettings["Entorno"];
				string appName = cred.nombAplicacion != null ? (string)cred.nombAplicacion : "Desconocida";
				var appsPermitidas = CorsSettings.GetAppsPermitidas(entorno);

				string tiempoMinutosVigente = cred.TiempoActivo != null ? cred.TiempoActivo.ToString() : "15";
				int tiempoActivo = int.Parse(tiempoMinutosVigente);

				if (!appsPermitidas.Any(a => a.Equals(appName, StringComparison.OrdinalIgnoreCase)))
				{
					string detalle = $"Aplicación '{appName}' no autorizada para generar JWT en {entorno}";
					auditLogger.Warn(detalle);
					WriteAudit(appName, "GenerateJwt", detalle);

					return Content(HttpStatusCode.Forbidden, new { mensaje = detalle });
				}

				if (cred.Usuario == null || cred.Ambiente == null)
				{
					auditLogger.Warn("Usuario y Ambiente son obligatorios para generar el JWT");
					WriteAudit(appName, "GenerateJwt", "Usuario y Ambiente no especificados");
					return BadRequest("Usuario y Ambiente son obligatorios para generar el JWT.");
				}

				var issuer = ConfigurationManager.AppSettings["JwtIssuer"];
				var audience = ConfigurationManager.AppSettings["JwtAudience"];
				var secretKeyBase64 = ConfigurationManager.AppSettings["JwtSecretKey"];
				var secretKeyBytes = Encriptacion.DecryptLongKey(secretKeyBase64);

				var securityKey = new SymmetricSecurityKey(secretKeyBytes);
				var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

				var secToken = new JwtSecurityToken(
					issuer: issuer,
					audience: audience,
					claims: new[]
					{
				new Claim(ClaimTypes.Name, (string)cred.Usuario),
				new Claim("amb", cred.Ambiente.ToString()),
				new Claim("app", appName)
					},
					expires: DateTime.UtcNow.AddMinutes(tiempoActivo),
					signingCredentials: credentials
				);

				var handler = new JwtSecurityTokenHandler();

				// No se registra en auditoría el éxito, solo errores
				return Ok(new { Token = handler.WriteToken(secToken) });
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, $"Error al generar JWT para aplicación '{cred?.nombAplicacion ?? "Desconocida"}'");
				WriteAudit(cred?.nombAplicacion ?? "Desconocida", "GenerateJwt", $"Error: {ex.Message}");
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Valida un token JWT recibido en el encabezado HTTP.
		/// </summary>
		/// <remarks>
		/// El token debe enviarse en el header "Authorization" con el formato:
		/// Authorization: Bearer eyJhbGciOi...
		/// Ejemplo:
		/// GET api/wsLogs/validateJwt
		/// Header: Authorization: Bearer eyJhbGciOi...
		/// Respuesta: objeto con estatus y mensajes.
		/// </remarks>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el resultado de la validación.
		/// Devuelve 400 BadRequest si el header no está presente.
		/// Devuelve 403 Forbidden si la aplicación no está autorizada.
		/// Devuelve 401 Unauthorized si el token es inválido.
		/// Devuelve 200 Ok si el token es válido.
		/// </returns>
		[HttpGet]
		[Route("validateJwt")]
		public IHttpActionResult ValidateJwt()
		{
			auditLogger.Info("Entrando a ValidateJwt");

			try
			{
				var headers = Request.Headers;
				if (!headers.Contains("Authorization"))
				{
					auditLogger.Warn("Header 'Authorization' no encontrado en ValidateJwt");
					WriteAudit("JWT", "ValidateJwt", "Header 'Authorization' no encontrado");
					return BadRequest("El header 'Authorization' es obligatorio.");
				}

				string token = headers.GetValues("Authorization").FirstOrDefault()?.Replace("Bearer ", "");
				if (string.IsNullOrWhiteSpace(token))
				{
					auditLogger.Warn("Header 'Authorization' vacío en ValidateJwt");
					WriteAudit("JWT", "ValidateJwt", "Header 'Authorization' vacío");
					return BadRequest("El header 'Authorization' no puede estar vacío.");
				}

				var secretKeyBase64 = ConfigurationManager.AppSettings["JwtSecretKey"];
				var secretKeyBytes = Encriptacion.DecryptLongKey(secretKeyBase64);

				var issuer = ConfigurationManager.AppSettings["JwtIssuer"];
				var audience = ConfigurationManager.AppSettings["JwtAudience"];

				var tokenHandler = new JwtSecurityTokenHandler();
				var validationParameters = new TokenValidationParameters
				{
					ValidateLifetime = true,
					ValidateAudience = true,
					ValidateIssuer = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
					ClockSkew = TimeSpan.Zero // elimina tolerancia de 5 minutos por defecto
				};

				SecurityToken validatedToken;
				var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

				// Extraer claim de aplicación
				var jwtToken = validatedToken as JwtSecurityToken;
				string appName = jwtToken?.Claims.FirstOrDefault(c => c.Type == "app")?.Value ?? "Desconocida";

				string entorno = ConfigurationManager.AppSettings["Entorno"];
				var appsPermitidas = CorsSettings.GetAppsPermitidas(entorno);

				if (!appsPermitidas.Any(a => a.Equals(appName, StringComparison.OrdinalIgnoreCase)))
				{
					string detalle = $"Aplicación '{appName}' no autorizada para usar JWT en {entorno}";
					auditLogger.Warn(detalle);
					WriteAudit(appName, "ValidateJwt", detalle);

					return Content(HttpStatusCode.Forbidden, new { mensaje = detalle });
				}

				// No se registra en auditoría el éxito, solo errores
				return Ok(new { mensaje = "Token válido", usuario = principal.Identity.Name, aplicacion = appName });
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al validar token JWT");
				WriteAudit("JWT", "ValidateJwt", $"Error al validar token: {ex.Message}");
				return Content(HttpStatusCode.Unauthorized, new { mensaje = "Token inválido", detalle = ex.Message });
			}
		}

		/// <summary>
		/// Refresca un token JWT utilizando un refresh token válido.
		/// </summary>
		/// <remarks>
		/// El refresh token debe enviarse en el body de la petición.
		/// Ejemplo:
		/// POST api/wsLogs/refreshJwt
		/// Body: "eyJhbGciOi...refresh"
		/// Respuesta: { "AccessToken": "eyJhbGciOi...nuevo" }
		/// </remarks>
		/// <param name="refreshToken">Token JWT de tipo refresh.</param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el nuevo access token si el refresh token es válido.
		/// Devuelve 401 Unauthorized si el refresh token es inválido.
		/// Devuelve 500 InternalServerError si ocurre un error inesperado.
		/// </returns>
		[HttpPost]
		[Route("refreshJwt")]
		public IHttpActionResult RefreshJwt([FromBody] string refreshToken)
		{
			auditLogger.Info("Entrando a RefreshJwt");

			try
			{
				if (string.IsNullOrWhiteSpace(refreshToken))
				{
					auditLogger.Warn("Refresh token vacío en RefreshJwt");
					WriteAudit("JWT", "RefreshJwt", "Refresh token vacío");
					return BadRequest("El refresh token no puede estar vacío.");
				}

				var secretKeyBase64 = ConfigurationManager.AppSettings["JwtSecretKey"];
				var secretKeyBytes = Encriptacion.DecryptLongKey(secretKeyBase64);

				var issuer = ConfigurationManager.AppSettings["JwtIssuer"];
				var audience = ConfigurationManager.AppSettings["JwtAudience"];

				var tokenHandler = new JwtSecurityTokenHandler();
				var validationParameters = new TokenValidationParameters
				{
					ValidateLifetime = true,
					ValidateAudience = true,
					ValidateIssuer = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
					ClockSkew = TimeSpan.Zero
				};

				SecurityToken validatedToken;
				var principal = tokenHandler.ValidateToken(refreshToken, validationParameters, out validatedToken);

				// Verificar que sea refresh token
				var jwt = validatedToken as JwtSecurityToken;
				if (jwt == null || !jwt.Claims.Any(c => c.Type == "type" && c.Value == "refresh"))
				{
					auditLogger.Warn("Token recibido no es de tipo refresh");
					WriteAudit("JWT", "RefreshJwt", "Token inválido: no es refresh");
					return Unauthorized();
				}

				// Generar nuevo access token
				var credentials = new SigningCredentials(new SymmetricSecurityKey(secretKeyBytes), SecurityAlgorithms.HmacSha512);
				var newAccessToken = new JwtSecurityToken(
					issuer: issuer,
					audience: audience,
					claims: principal.Claims,
					expires: DateTime.UtcNow.AddMinutes(15),
					signingCredentials: credentials
				);

				string newAccessTokenString = tokenHandler.WriteToken(newAccessToken);

				// No se registra en auditoría el éxito, solo errores
				return Ok(new { AccessToken = newAccessTokenString });
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al refrescar token JWT");
				WriteAudit("JWT", "RefreshJwt", $"Error al refrescar token: {ex.Message}");
				return Content(HttpStatusCode.Unauthorized, new { mensaje = "Refresh token inválido", detalle = ex.Message });
			}
		}

		/// <summary>
		/// Extiende la vigencia de un token JWT válido por 15 minutos adicionales.
		/// </summary>
		/// <remarks>
		/// El token debe enviarse en el body de la petición.
		/// Ejemplo:
		/// POST api/wsLogs/extendJwt
		/// Body: "eyJhbGciOi...token"
		/// Respuesta: { "Token": "eyJhbGciOi...nuevo" }
		/// </remarks>
		/// <param name="oldToken">El token JWT que se desea extender.</param>
		/// <returns>
		/// Un <see cref="IHttpActionResult"/> con el nuevo token extendido.
		/// Devuelve 400 BadRequest si el token está vacío.
		/// Devuelve 401 Unauthorized si el token es inválido o expirado.
		/// Devuelve 200 Ok si el token se extendió correctamente.
		/// </returns>
		[HttpPost]
		[Route("extendJwt")]
		public IHttpActionResult ExtendJwt([FromBody] string oldToken)
		{
			auditLogger.Info("Entrando a ExtendJwt");

			try
			{
				if (string.IsNullOrWhiteSpace(oldToken))
				{
					auditLogger.Warn("Token vacío en ExtendJwt");
					WriteAudit("JWT", "ExtendJwt", "Token vacío recibido");
					return BadRequest("El token no puede estar vacío.");
				}

				var secretKeyBase64 = ConfigurationManager.AppSettings["JwtSecretKey"];
				var secretKeyBytes = Encriptacion.DecryptLongKey(secretKeyBase64);

				var issuer = ConfigurationManager.AppSettings["JwtIssuer"];
				var audience = ConfigurationManager.AppSettings["JwtAudience"];

				var tokenHandler = new JwtSecurityTokenHandler();
				var validationParameters = new TokenValidationParameters
				{
					ValidateLifetime = true,
					ValidateAudience = true,
					ValidateIssuer = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
					ClockSkew = TimeSpan.Zero
				};

				SecurityToken validatedToken;
				var principal = tokenHandler.ValidateToken(oldToken, validationParameters, out validatedToken);

				// Generar nuevo token con +15 minutos
				var credentials = new SigningCredentials(new SymmetricSecurityKey(secretKeyBytes), SecurityAlgorithms.HmacSha512);
				var newToken = new JwtSecurityToken(
					issuer: issuer,
					audience: audience,
					claims: principal.Claims,
					expires: DateTime.UtcNow.AddMinutes(15),
					signingCredentials: credentials
				);

				string newTokenString = tokenHandler.WriteToken(newToken);

				// No se registra en auditoría el éxito, solo errores
				return Ok(new { Token = newTokenString });
			}
			catch (Exception ex)
			{
				auditLogger.Error(ex, "Error al extender token JWT");
				WriteAudit("JWT", "ExtendJwt", $"Error al extender token: {ex.Message}");
				return Content(HttpStatusCode.Unauthorized, new { mensaje = "Token inválido o expirado", detalle = ex.Message });
			}
		}


		private static readonly object auditLock = new object();

		/// <summary>
		/// Registra una entrada en el archivo de auditoría.
		/// </summary>
		/// <param name="appName">Nombre de la aplicación asociada.</param>
		/// <param name="action">Acción realizada.</param>
		/// <param name="detalle">Detalle del evento o error.</param>
		private void WriteAudit(string appName, string action, string detalle)
		{
			try
			{
				string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - App: {appName} - Acción: {action} - Detalle: {detalle}";
				auditLogger.Info(entry);

				string auditFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Log/auditoria.log");

				lock (auditLock) // asegura que solo un hilo escriba a la vez
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

		private void WriteAudit_v01(string appName, string action, string detalle)
		{
			try
			{
				string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - App: {appName} - Acción: {action} - Detalle: {detalle}";
				auditLogger.Info(entry);

				string auditFilePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Log/auditoria.log");
				if (!File.Exists(auditFilePath))
				{
					using (File.Create(auditFilePath)) { }
				}
				File.AppendAllText(auditFilePath, entry + Environment.NewLine);
			}
			catch (Exception ex)
			{
				// En caso de fallo al escribir en auditoría, se registra en el logger
				auditLogger.Error(ex, "Error al escribir en auditoría");
			}
		}

	}

}