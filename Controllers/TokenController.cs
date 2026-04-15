using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace swLog.Controllers
{
	/// <summary>
	/// Proporciona puntos finales para generar y validar tokens utilizados para habilitar entornos de contingencia.
	/// </summary>
	/// <remarks>The <see cref="TokenController"/> class exposes methods to generate tokens with a fixed expiration
	/// time  and validate their validity. Tokens are used as keys to access contingency environments, which require  valid
	/// credentials for entry. The generated tokens have a lifespan of 8 hours.</remarks>
	public class TokenController : ApiController
	{
		private static Dictionary<string, DateTime> tokens = new Dictionary<string, DateTime>();

		// Método para generar un token aleatorio de 20 caracteres en UTF-8
		private string GenerateRandomToken(int length)
		{
			const string chars = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyz0123456789";
			Random random = new Random();
			return new string(Enumerable.Repeat(chars, length)
				.Select(s => s[random.Next(s.Length)]).ToArray());
		}

		/// <summary>
		///Endpoint para generar un token con tiempo de vida de 8 horas.
		/// </summary>
		/// <remarks>
		/// Genera un Token que será la llave para habilitar el ambiente de contingencia. Cabe mencionar que para poder ingresar a contingencia se debe de contar con un usuario y contraseña válido.
		/// El tojen tiene un tiempo de vida de 8 horas.
		/// </remarks>
		/// <response code="200">Token generado exitosamente.</response>
		/// <response code="500">Error interno del servidor.</response>
		[HttpPost]
		[Route("api/token-Si_Notarios/generate")]
		public IHttpActionResult GenerateToken()
		{
			try
			{
				string token = GenerateRandomToken(20); // Token de 20 caracteres
				DateTime expirationTime = DateTime.Now.AddHours(8);

				// Guardar el token y su tiempo de expiración
				tokens[token] = expirationTime;

				return Ok(new { Token = token, Expiration = expirationTime });
			}
			catch (Exception ex)
			{
				return InternalServerError(ex);
			}
		}

		/// <summary>
		/// Endpoint para validar un token.
		/// </summary>
		/// <remarks>
		/// Este endpoint verifica si el token proporcionado es válido y no ha expirado.
		/// </remarks>
		/// <param name="token">El token que se desea validar.</param>
		/// <response code="200">Token válido o inválido.</response>
		/// <response code="400">El token es inválido o está vacío.</response>
		[HttpGet]
		[Route("api/token-Si_Notarios/validate/{token}")]
		public IHttpActionResult ValidateToken(string token) // Eliminar [FromBody]
		{
			try
			{
				if (string.IsNullOrWhiteSpace(token))
					return BadRequest("El token no puede estar vacío.");

				if (tokens.ContainsKey(token))
				{
					DateTime expirationTime = tokens[token];

					if (DateTime.Now <= expirationTime)
					{
						return Ok(true); // Token válido
					}
					else
					{
						// Token expirado, eliminar de la lista
						tokens.Remove(token);
						return Ok(false);
					}
				}
				else
				{
					return Ok(false); // Token no encontrado o inválido
				}
			}
			catch (Exception ex)
			{
				return InternalServerError(ex);
			}
		}
	}
}