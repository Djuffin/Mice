using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class People<T> where T:Person
	{
		private List<T> _data = new List<T>();

		public People()
		{
		}

		public People(T[] items)
		{
			_data.AddRange(items);
		}

		public void Add(T item)
		{
			_data.Add(item);
		}

		public void AddRange(IEnumerable<T> items)
		{
			_data.AddRange(items);
		}

		public IEnumerable<T1> Cast<T1>()
		{
			return _data.Cast<T1>();
		}

		public void AddRange2<T1> (IEnumerable<T1> items) where T1:T
		{
			AddRange(items.Cast<T>());
		}

	}
}
