using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public enum Rank
	{
		Sergeant,
		Lieutenant,
		Captain,
		Major,
		General
	}

	public class Soldier : Person
	{

		private Rank _rank;
		public Rank Rank
		{
			get { return _rank; }
			set { _rank = value; }
		}

		public override void Kill()
		{
			base.Kill();
			_rank = Rank.General; 
		}
	}
}
