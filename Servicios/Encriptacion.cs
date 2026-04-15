using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace swLog.Servicios
{
	internal class Encriptacion
	{
		private const int KeySize = 256;
		private const int Iteraciones = 10000;
		private static readonly byte[] Salt = Encoding.UTF8.GetBytes("$3rv1c10s-L0gf1n4n"); // Puedes personalizarlo
		private static readonly byte[] IV = Encoding.UTF8.GetBytes("1N1C14L_V3CT0R_N"); // Debe ser de 16 bytes

		public static string DecryptString(string cipherText, string passPhrase)
		{
			using (var aes = Aes.Create())
			{
				var key = new Rfc2898DeriveBytes(passPhrase, Salt, Iteraciones);
				aes.Key = key.GetBytes(KeySize / 8);
				aes.IV = IV;
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;

				byte[] cipherBytes = Convert.FromBase64String(cipherText);
				using (var memStream = new MemoryStream(cipherBytes))
				using (var cryptoStream = new CryptoStream(memStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
				using (var reader = new StreamReader(cryptoStream, Encoding.UTF8))
				{
					return reader.ReadToEnd();
				}
			}
		}
		public static byte[] DecryptLongKey(string base64Key)
		{
			return Convert.FromBase64String(base64Key);
		}
	}
}