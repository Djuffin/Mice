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

		public T Get(int index)
		{
			return _data[index];
		}

		public T this[int index]
		{
			get { return _data[index]; }
		}

		public void AddRange(IEnumerable<T> items)
		{
			_data.AddRange(items);
		}

		public IEnumerable<T1> Cast<T1>()
		{
			return _data.Cast<T1>();
		}

		public static List<T1> StaticCast<T1>()
		{
			return new List<T1>();
		}

		public int AddRange2<T1>(IEnumerable<T1> items) where T1 : T
		{
			AddRange(items.Cast<T>());
			return 1;
		}

		public void AddRange2ViodReturn<T1>(IEnumerable<T1> items) where T1 : T
		{
			AddRange(items.Cast<T>());
		}

	}
}
