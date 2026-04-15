using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace swLog.Models
{
	public class Mensaje
	{

		public int EstatusEjecucion { get; set; }
		public String MensajeCiudadano { get; set; }
		public String MensajeTecnico { get; set; }

		public Mensaje() { }

		public Mensaje(int estatusEjecucion, String mensajeCiudadano)
		{
			this.EstatusEjecucion = estatusEjecucion;
			this.MensajeCiudadano = mensajeCiudadano;
		}

		public Mensaje(int estatusEjecucion, String mensajeCiudadano, String mensajeTecnico)
		{
			this.EstatusEjecucion = estatusEjecucion;
			this.MensajeCiudadano = mensajeCiudadano;
			this.MensajeTecnico = mensajeTecnico;
		}

		public String ToStringMens()
		{
			return $"{EstatusEjecucion}/{MensajeTecnico}/{MensajeCiudadano}";
		}
	}
}