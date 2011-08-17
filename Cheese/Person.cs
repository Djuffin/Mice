using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Person
	{
		private bool _isAlive;
		public bool IsAlive
		{
			get
			{
				return _isAlive;
			}
		}

		private string _name;
		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public Person()
		{
			//System.Diagnostics.Debugger.Break();
			System.Diagnostics.Debugger.Log(1, "ad", "Person has been created");
			_isAlive = true;
		}

		public void Kill()
		{
			_isAlive = false;
		}
	}
}
