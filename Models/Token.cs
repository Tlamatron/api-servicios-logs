using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace swLog.Models
{
	/// <summary>
	/// Identidad Token es una clase que proporciona métodos para crear y validar tokens de autenticación en el contexto de un sistema de logging.
	/// </summary>
	public class Token
	{
		/// <summary>
		/// Método estático CrearToken que genera un token de autenticación basado en la información proporcionada, como el motor, el ambiente, el usuario, el nombre de la aplicación y el tiempo de actividad. El token se crea concatenando esta información y luego encriptándola para garantizar su seguridad.
		/// </summary>
		/// <param name="motor"></param>
		/// <param name="amb"></param>
		/// <param name="user"></param>
		/// <param name="nApp"></param>
		/// <param name="timeAct"></param>
		/// <returns></returns>
		public static String CrearToken(int motor, int amb, String user, String nApp, int timeAct)
		{
			String time = DateTime.Now.ToString("dd/MM/yy HH:mm:ss");
			String sep = " ";
			String ans = time + sep + user + sep + motor + sep + amb + sep + nApp + sep + timeAct.ToString();
			Debug.WriteLine(ans);
			return Seguridad.Encriptar(ans);
		}
		/// <summary>
		/// método que valida el ambiente recibido en el token, asegurándose de que esté dentro de los valores permitidos (0 para pruebas, 1 para contingencia y 2 para producción). Si el valor del ambiente es menor que 0 o mayor que 2, la función devuelve false, indicando que el ambiente no es válido. En caso contrario, devuelve true, indicando que el ambiente es válido.
		/// </summary>
		/// <param name="amb"></param>
		/// <returns></returns>
		public static bool validarAmbiente(int amb)/*0 Pruebas, 1 contingencia y 2 Producción */
		{
			int ambiente = Convert.ToInt16(amb);

			if (ambiente < 0)
			{
				return false;
			}
			if (ambiente > 2)
			{
				return false;
			}
			return true;

		}
		/// <summary>
		/// Validar es un método que se encarga de validar un token de autenticación. El proceso de validación implica desencriptar el token para extraer la información contenida en él, como la fecha, hora, usuario, motor, ambiente, nombre de la aplicación y tiempo activo. Luego, se verifica si el ambiente es válido utilizando el método validarAmbiente. Si el ambiente no es válido, se devuelve un mensaje indicando que el ambiente no es válido. Si el ambiente es válido, se verifica la vigencia del token utilizando los métodos Vigencia. Si el token es válido y vigente, se devuelve un objeto UserConection con la información del usuario y un mensaje de éxito. Si el token no es válido o ha expirado, se devuelve un mensaje indicando que el token es inválido o ha expirado.
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public static UserConection Validar(String token)
		{
			UserConection ans = new UserConection(new Mensaje(1, ""));
			String cad = Seguridad.Desencriptar(token);
			String[] words = cad.Split(' ');
			for (int i = 0; i < words.Length; i++)
				Debug.WriteLine(i + "=" + words[i]);

			if (words.Length < 7)
			{
				return new UserConection(new Mensaje(-400, "Token Invalido, error al desencriptar "));
			}
			ans.ConfigUser(Int16.Parse(words[3]), Int16.Parse(words[4]), words[2], words[5], Int16.Parse(words[6]));
			if (!validarAmbiente(Int16.Parse(words[4])))
			{
				Debug.WriteLine("Usuario de Pruebas");
				return new UserConection(new Mensaje(-407, "Ambiente no válido"));
			}
			if (string.IsNullOrEmpty(words[6]) || words[6] == "0")
			{
				if (Vigencia(words[0], words[1]))
				{
					Debug.WriteLine("Conexion Valida");
					return ans;
				}
			}
			else
			{
				if (Vigencia(words[0], words[1], words[6]))
				{
					Debug.WriteLine("Conexion Valida");
					return ans;
				}
			}
			return new UserConection(new Mensaje(-400, "Token expirado"));
		}
		/// <summary>
		/// Valida la vigencia de un token comparando la fecha y hora actual con la fecha y hora contenida en el token. El método toma dos parámetros: Day, que representa la fecha en formato "dd/MM/yy", y Time, que representa la hora en formato "HH:mm:ss". Primero, se verifica si la fecha del token coincide con la fecha actual. Si no coinciden, se considera que el token es de otro día y se devuelve false. Luego, se extraen las horas, minutos y segundos del token para crear un objeto DateTime que representa el momento en que se generó el token. Se calcula la diferencia de tiempo entre la fecha y hora actual y la fecha y hora del token. Si esta diferencia es mayor a 13 minutos, se considera que el token ha expirado y se devuelve false. En caso contrario, se devuelve true, indicando que el token es válido.
		/// </summary>
		/// <param name="Day"></param>
		/// <param name="Time"></param>
		/// <returns></returns>
		public static Boolean Vigencia(String Day, String Time)
		{

			DateTime now = DateTime.Now;
			if (Day.CompareTo(now.ToString("dd/MM/yy")) != 0)
			{
				Debug.WriteLine("El Token es de otro dia");
				return false;
			}
			int hr = int.Parse(Time.Substring(0, 2));
			int mn = int.Parse(Time.Substring(3, 2));
			int ss = int.Parse(Time.Substring(6, 2));

			DateTime TokenTime = new DateTime(now.Year, now.Month, now.Day, hr, mn, ss);
			Debug.WriteLine("  now:" + now.ToString("dd/MM/yy HH:mm:ss"));
			Debug.WriteLine("Token:" + TokenTime.ToString("dd/MM/yy HH:mm:ss"));


			TimeSpan ts = now - TokenTime;
			Debug.WriteLine("Tiempo de vida del Token:" + ts);

			if (ts.TotalMinutes > 13)
				return false;

			return true;
		}
		/// <summary>
		/// Valida la vigencia de un token comparando la fecha y hora actual con la fecha y hora contenida en el token, teniendo en cuenta un tiempo de actividad específico. El método toma tres parámetros: Day, que representa la fecha en formato "dd/MM/yy", Time, que representa la hora en formato "HH:mm:ss", y timeActivo, que representa el tiempo de actividad permitido en minutos. Primero, se verifica si la fecha del token coincide con la fecha actual. Si no coinciden, se considera que el token es de otro día y se devuelve false. Luego, se extraen las horas, minutos y segundos del token para crear un objeto DateTime que representa el momento en que se generó el token. Se calcula la diferencia de tiempo entre la fecha y hora actual y la fecha y hora del token. Si esta diferencia es mayor al tiempo de actividad permitido (timeActivo), se considera que el token ha expirado y se devuelve false. En caso contrario, se devuelve true, indicando que el token es válido.
		/// </summary>
		/// <param name="Day"></param>
		/// <param name="Time"></param>
		/// <param name="timeActivo"></param>
		/// <returns></returns>
		public static Boolean Vigencia(String Day, String Time, String timeActivo)
		{
			int tiempoActivo = int.Parse(timeActivo);
			DateTime now = DateTime.Now;
			if (Day.CompareTo(now.ToString("dd/MM/yy")) != 0)
			{
				Debug.WriteLine("El Token es de otro dia");
				return false;
			}
			int hr = int.Parse(Time.Substring(0, 2));
			int mn = int.Parse(Time.Substring(3, 2));
			int ss = int.Parse(Time.Substring(6, 2));

			DateTime TokenTime = new DateTime(now.Year, now.Month, now.Day, hr, mn, ss);
			Debug.WriteLine("  now:" + now.ToString("dd/MM/yy HH:mm:ss"));
			Debug.WriteLine("Token:" + TokenTime.ToString("dd/MM/yy HH:mm:ss"));


			TimeSpan ts = now - TokenTime;
			Debug.WriteLine("Tiempo de vida del Token:" + ts);

			if (ts.TotalMinutes > tiempoActivo)
				return false;

			return true;
		}
	}
}