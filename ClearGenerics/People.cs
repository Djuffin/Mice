using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClearGenerics
{
	public class People<T> where T: Person
	{
		private List<T> _data = new List<T>();

		public static PeoplePrototype StaticPrototype;

		public PeoplePrototype TestMethod()
		{
			return new PeoplePrototype();
		}

		public T TestField;

		public struct PeoplePrototype
		{

			public Add_CallBack Add;
			public delegate void Add_CallBack(T person);

			public Dictionary<Type, object> setAddRangeActions;

			public void SetAddRange2<T1, T2>(Func<T1, T2> action)
			{
				if (setAddRangeActions == null)
				{
					setAddRangeActions = new Dictionary<Type, object>();
				}
				setAddRangeActions[typeof(Func<T1, T2>)] = action;
			}

			public Func<T1, T2> GetAddRange2<T1, T2>()
			{
				if (setAddRangeActions.ContainsKey(typeof(Func<T1, T2>)))
				{
					return (Func<T1, T2>)setAddRangeActions[typeof(Func<T1, T2>)];
				}
				return null;
			}
		}

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
