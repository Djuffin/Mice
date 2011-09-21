using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cheese;


namespace Tester
{
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
			People<Person> people = new People<Person>();
			people.Prototype = new People<Person>.PrototypeClass()
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
				Ctor = (self) =>
				{
					var entity = GetCache(self);
					entity.Set(EntitiesKey, new List<Person>());
				},
				Ctor_TArray = (self, itemsArray) =>
				{
					var entity = GetCache(self);
					List<Person> entities = new List<Person>();
					entities.AddRange(itemsArray);
					entity.Set(EntitiesKey, entities);
				}
			};
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
				Ctor = (self) =>
				{
					var entity = GetCache(self);
					entity.Set(EntitiesKey, new List<Person>());
				},
				Ctor_TArray = (self, itemsArray) =>
				{
					var entity = GetCache(self);
					List<Person> entities = new List<Person>();
					entities.AddRange(itemsArray);
					entity.Set(EntitiesKey, entities);
				}
			};
		}

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
