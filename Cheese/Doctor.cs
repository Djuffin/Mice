using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Doctor : Person
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

		private Person[] _patients = new Person[0];

		public Person[] Patients
		{
			get { return _patients; }
			set { _patients = value; }
		}

		public void AddPatients(Person p)
		{
			_patients = _patients.Concat(new[] {p}).ToArray();
		}

		public void AddPatients(Person[] ps)
		{
			_patients = _patients.Concat(ps).ToArray();
		}
	}
}
