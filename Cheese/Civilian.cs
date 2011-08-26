using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Civilian : Person
	{
		private Civilian()
		{
			
		}

		public string SayHello(string name)
		{
			return "Hello " + name;
		}
	}
}
