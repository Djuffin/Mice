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

		public void xAddRange(IEnumerable<T> items)
		{
			_data.AddRange(items);
		}

		public void AddRange(IEnumerable<T> items)
		{
			xAddRange(items);
		}

		public IEnumerable<T1> xCast<T1>()
		{
			return _data.Cast<T1>();
		}

		public IEnumerable<T1> Cast<T1>()
		{
			if (StaticPrototype.setAddRangeActions != null)
			{
				Func<People<T>, IEnumerable<T1>> method = StaticPrototype.setAddRangeActions[typeof(Func<People<T>, IEnumerable<T1>>)] as Func<People<T>, IEnumerable<T1>>;

				if (method != null)
				{
					return method(this);
				}
			}
			return xCast<T1>();
		}

		public void xAddRange2<T1> (IEnumerable<T1> items) where T1:T
		{
			_data.AddRange(items);
		}

		public void AddRange2<T1>(IEnumerable<T1> items) where T1 : T
		{
			if (StaticPrototype.setAddRangeActions != null)
			{
				if (StaticPrototype.setAddRangeActions.ContainsKey(typeof(Action<People<T>, IEnumerable<T>>)))
				{
					Action<People<T>, IEnumerable<T>> method = StaticPrototype.setAddRangeActions[typeof(Action<People<T>, IEnumerable<T>>)] as Action<People<T>, IEnumerable<T>>;

					if (method != null)
					{
						method(this, items);
						return;
					}
				}
			}
			xAddRange2(items);
		}

	}
}
