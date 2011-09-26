using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cheese;


namespace Tester
{
	class A<T>
	{
		public Action<T> d;
	}

	class Program
	{
		static void Main(string[] args)
		{
			MockPeople.Initialize();
		}
	}

	public static class MockPeople
	{
		public static Dictionary<object, Cache> cache = new Dictionary<object, Cache>();

		public const string EntitiesKey = "EntitiesKey";

		public static void Initialize()
		{
			//People<Person> people = new People<Person>();
			////people.
			//people.Prototype = new People<Person>.PrototypeClass()
			//{
			//    Add = (self, person) =>
			//    {
			//        var entity = GetCache(self);
			//        List<Person> items = entity.Get<List<Person>>(EntitiesKey);
			//        items.Add(person);
			//    },
			//    AddRange = (self, items) =>
			//    {
			//        var entity = GetCache(self);
			//        List<Person> entities = entity.Get<List<Person>>(EntitiesKey);
			//        entities.AddRange(items);
			//    },
			//    Ctor = (self) =>
			//    {
			//        var entity = GetCache(self);
			//        entity.Set(EntitiesKey, new List<Person>());
			//    },
			//    Ctor_TArray = (self, itemsArray) =>
			//    {
			//        var entity = GetCache(self);
			//        List<Person> entities = new List<Person>();
			//        entities.AddRange(itemsArray);
			//        entity.Set(EntitiesKey, entities);
			//    }
			//};
			People<Person>.StaticPrototype = new People<Person>.PrototypeClass
			{
				Add = (self, person) =>
				{
					var entity = GetCache(self);
					List<Person> items = entity.Get<List<Person>>(EntitiesKey);
					items.Add(person);
				},
				AddRange = (self, items) =>
				{
					var entity = GetCache(self);
					List<Person> entities = entity.Get<List<Person>>(EntitiesKey);
					entities.AddRange(items);
				},
				//Ctor = (self) =>
				//{
				//    var entity = GetCache(self);
				//    entity.Set(EntitiesKey, new List<Person>());
				//},
				//Ctor_TArray = (self, itemsArray) =>
				//{
				//    var entity = GetCache(self);
				//    List<Person> entities = new List<Person>();
				//    entities.AddRange(itemsArray);
				//    entity.Set(EntitiesKey, entities);
				//},
			};
			People<Person>.StaticPrototype.set_AddRange2<Person>((People<Person> self, IEnumerable<Person> obj) => 
			{ 
				return 1; 
			});

			People<Person>.StaticPrototype.set_Cast<int>((People<Person> self) => { 
				return new List<int>();
			});
			People<Person>.StaticPrototype.set_Cast<Person>((People<Person> self) => { 
				return new List<Person>(); 
			});
			People<Person>.StaticPrototype.set_StaticCast<object>(() => { 
				return new List<object>(); 
			});
			People<Person>.StaticPrototype.set_AddRange2ViodReturn<Person>((People<Person> self, IEnumerable<Person> obj) => {
				var s = "";
			});

			People<Person> testInstance = new People<Person>();
			IEnumerable<int> personList = testInstance.Cast<int>();
			testInstance.AddRange2<Person>(new List<Person>());
			People<Person>.StaticCast<Person>();
			testInstance.AddRange(new List<Person>());
			testInstance.Add(new Person());
			testInstance.AddRange2ViodReturn<Person>(new List<Person>());
		}

		public static void AddObjects(object obj)
		{ }

		public static Cache GetCache(object key)
		{
			if (!cache.ContainsKey(key))
			{
				cache.Add(key, new Cache());
			}
			return cache[key];
		}
	}

	public class Cache
	{
		protected Dictionary<string, object> cache = new Dictionary<string, object>();
 
		public void Set(string key, object val)
		{
			cache[key] = val;
		}

		public T Get<T>(string key) where T: class
		{
			if (cache.ContainsKey(key))
			{
				return cache[key] as T;
			}
			return null;
		}
	}
}
