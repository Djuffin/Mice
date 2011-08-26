using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Exceptions
	{
		public void ThrowException(Exception e)
		{
			throw e;
		}

		public static bool CatchException()
		{
			try
			{
				new Exceptions().ThrowException(new InvalidOperationException());
			}
			catch (InvalidOperationException e)
			{
				return true;
			}
			return false;
		}
	}
}
