using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace swLog.Models
{
	public class Seguridad
	{
		private static string key { get; set; } = "f1n4nz4$Pu#bl@";
		public static Usuario ValidarToken(String token)
		{
			Usuario resultado = new Usuario();
			var tokenHandler = new JwtSecurityTokenHandler();
			var validationParameters = GetValidationParameters();
			SecurityToken validatedToken;
			try
			{
				IPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);
				Thread.CurrentPrincipal = principal;
				resultado.EstatusEjecucion = 1;
			}
			catch (SecurityTokenExpiredException te)
			{
				System.Diagnostics.Debug.WriteLine("Error --->" + te.ToString());
				resultado.EstatusEjecucion = -700;
				resultado.MensajeCiudadano = "TOKEN CADUCADO";
				resultado.MensajeTecnico = "EXPIRED TOKEN";
			}
			catch (SecurityTokenInvalidSignatureException se)
			{
				System.Diagnostics.Debug.WriteLine("Error --->" + se.ToString());
				resultado.EstatusEjecucion = -701;
				resultado.MensajeCiudadano = "TOKEN INVALIDO EN SU FIRMA";
				resultado.MensajeTecnico = "INVALID TOKEN";
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("Error --->" + ex.ToString());
				resultado.EstatusEjecucion = -702;
				resultado.MensajeCiudadano = "TOKEN INVALIDO";
				resultado.MensajeTecnico = "MALFORMED TOKEN";
			}

			return resultado;
		}

		private static TokenValidationParameters GetValidationParameters()
		{
			return new TokenValidationParameters()
			{
				ValidateLifetime = false,
				ValidateAudience = false,
				ValidateIssuer = false,
				ValidIssuer = "54T$2023",
				ValidAudience = "Sample",
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
			};
		}

		public static string GenerateToken(String usuario, String password, int ambiente)
		{
			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);
			string ambConvert = ambiente.ToString();

			var secToken = new JwtSecurityToken(
				signingCredentials: credentials,
				issuer: "54T$2023",
				audience: "Sample",
				claims: new[]
				{
				new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name, usuario),
				new Claim(ambConvert, "amb"),
				new Claim(password, "pwd")
				}
			);

			var handler = new JwtSecurityTokenHandler();
			return handler.WriteToken(secToken);
		}

		public static bool validarContrasenia(String usuario, String password)
		{
			string hash = String.Empty;
			string hashConvert = String.Empty;

			// Initialize a SHA256 hash object
			using (SHA256 sha256 = SHA256.Create())
			{
				// Compute the hash of the given string
				byte[] hashValue = sha256.ComputeHash(Encoding.UTF8.GetBytes(usuario + "Mantenimiento"));

				// Convert the byte array to string format
				foreach (byte b in hashValue)
				{
					hash += $"{b:X2}";
				}
				hashConvert = hash.ToLower();
			}

			//System.Diagnostics.Debug.WriteLine("HASH:    " + hashConvert);
			return hashConvert.Equals(password);
		}

		public static String Encriptar(String value)
		{
			using (var md5 = new MD5CryptoServiceProvider())
			{
				using (var tdes = new TripleDESCryptoServiceProvider())
				{
					tdes.Key = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));
					tdes.Mode = CipherMode.ECB;
					tdes.Padding = PaddingMode.PKCS7;

					using (var transform = tdes.CreateEncryptor())
					{
						byte[] textBytes = UTF8Encoding.UTF8.GetBytes(value);
						byte[] bytes = transform.TransformFinalBlock(textBytes, 0, textBytes.Length);
						return Convert.ToBase64String(bytes, 0, bytes.Length);
					}
				}
			}
		}

		public static String Desencriptar(String value)
		{
			/// validar espacios
			/// l1c3nc1@D1G
			using (var md5 = new MD5CryptoServiceProvider())
			{
				using (var tdes = new TripleDESCryptoServiceProvider())
				{
					tdes.Key = md5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));
					tdes.Mode = CipherMode.ECB;
					tdes.Padding = PaddingMode.PKCS7;

					byte[] bytes = new byte[0];
					byte[] cipherBytes = new byte[0];

					using (var transform = tdes.CreateDecryptor())
					{
						try
						{
							cipherBytes = Convert.FromBase64String(value);
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine("ERROR Exception: => " + ex.ToString());
						}
						try
						{
							bytes = transform.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
						}
						catch (CryptographicException cex)
						{
							System.Diagnostics.Debug.WriteLine("ERROR Crypt: => " + cex.ToString());
						}
						catch (ArgumentNullException anex)
						{
							System.Diagnostics.Debug.WriteLine("ERROR ArgumentNull: => " + anex.ToString());
						}
						catch (ArgumentException aex)
						{
							System.Diagnostics.Debug.WriteLine("ERROR Argument: => " + aex.ToString());
						}
						return UTF8Encoding.UTF8.GetString(bytes);
					}
				}
			}
		}
	}
}