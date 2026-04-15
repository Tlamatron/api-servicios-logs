using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace swLog.Models
{
	/// <summary>
	/// Clase que representa la conexión de un usuario, incluyendo información sobre el motor, ambiente, usuario, aplicación y tiempo activo.
	/// </summary>
	public class UserConection : Mensaje
	{

		/// <summary>
		/// Hace referencia al motor utilizado en la conexión, 0 para Ingers, 1 para Sql Server, 2 para Oracle, 3 para MySQL, 4 para PostgreSQL, 5 para MongoDB, 6 para Redis, 7 para Cassandra, 8 para SQLite y 9 para DB2.
		/// </summary>
		public int Motor { get; set; } = 0;
		/// <summary>
		/// Hace referencia al ambiente utilizado en la conexión, 0 para pruenas, 1 para contingencia y 2 para producción.
		/// </summary>
		public int Ambiente { get; set; } = 0;
		/// <summary>
		/// Hace referencia al usuario que está utilizando la API, este campo es importante para identificar quién está realizando las solicitudes y para fines de auditoría y seguridad.
		/// </summary>
		public String User { get; set; } = "";
		/// <summary>
		/// Hace referencia al nombre de la aplicación que está utilizando la API, este campo es importante para identificar qué aplicación está realizando las solicitudes y para fines de auditoría y seguridad.
		/// </summary>
		public String nombAplicacion { get; set; } = "";
		/// <summary>
		/// Hace referencia al tiempo activo de la conexión, este campo es importante para identificar cuánto tiempo ha estado activa la conexión y para fines de auditoría y seguridad.
		/// </summary>
		public int TiempoActivo { get; set; } = 0;

		public UserConection() { }

		public UserConection(Mensaje msg)
		{
			this.EstatusEjecucion = msg.EstatusEjecucion;
			this.MensajeCiudadano = msg.MensajeCiudadano;
			this.MensajeTecnico = msg.MensajeTecnico;
		}

		public void ConfigUser(int Motor, int Ambiente, String User, String nombAplicacion, int TiempoActivo)
		{
			this.Motor = Motor;
			this.Ambiente = Ambiente;
			this.User = User;
			this.nombAplicacion = nombAplicacion;
			this.TiempoActivo = TiempoActivo;
		}
	}
}