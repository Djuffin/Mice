using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Doctor
	{
		private string _specialization;
		public string Specialization
		{
			get
			{
				return _specialization;
			}
		}

		public Doctor(string specialization)
		{
			_specialization = specialization;
		}
	}
}
