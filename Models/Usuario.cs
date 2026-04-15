using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace swLog.Models
{
	public class Usuario : Mensaje
	{
		public String usuario { get; set; }
		public String password { get; set; }
		public int ambiente { get; set; }
	}
}