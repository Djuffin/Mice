using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Cheese;
using System.Diagnostics;

namespace Mice.Tests
{
	[TestFixture]
	public class GenericsTests
	{

		[SetUp]
		public void SetUp()
		{
			Person.StaticPrototype = new Person.PrototypeClass();
			Soldier.StaticPrototype = new Soldier.PrototypeClass();
		}

		[Test]
		public void GenericClassCtor()
		{
			int ctorCallCout = 0;
			People<Soldier>.StaticPrototype = new People<Soldier>.PrototypeClass();
			People<Soldier>.StaticPrototype.Ctor = (self) =>
			{
				ctorCallCout++;
			};
			new People<Soldier>();
			new People<Doctor>();
			Assert.That(ctorCallCout, Is.EqualTo(1));
		}

		[Test]
		public void NotGenericInstanceMethod()
		{
			var p = new People<Doctor>();
			bool addWasCalled = false;
			p.People_1Prototype.Add = (self, item) =>
			{
				addWasCalled = true;
				p.xAdd(item);
			};
			p.Add(null);
			Assert.That(addWasCalled);
			Assert.That(p.Get(0), Is.Null);
		}

		[Test]
		public void GenericInstanceMethod()
		{
			var p = new People<Doctor>();
			bool soldierCastWasCalled = false;
			bool civilianCastWasCalled = false;
			People<Doctor>.StaticPrototype.set_Cast<Soldier>((self) =>
			{
				soldierCastWasCalled = true;
				return null;
			});
			p.People_1Prototype.set_Cast<Civilian>((self) =>
			{
				civilianCastWasCalled = true;
				return null;
			});
			Assert.That(p.Cast<Soldier>(), Is.Null);
			Assert.That(soldierCastWasCalled, Is.True);
			Assert.That(civilianCastWasCalled, Is.False);

			Assert.That(p.Cast<Civilian>(), Is.Null);
			Assert.That(soldierCastWasCalled, Is.True);
			Assert.That(civilianCastWasCalled, Is.True);
			
		}
	}
}
