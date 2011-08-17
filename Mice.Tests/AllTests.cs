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
	public class AllTests
	{
		[Test]
		public void Test1()
		{
			Person p = new Person();
			p.Kill();
			var isAlive = p.IsAlive;
			p.PersonPrototype.set_NameString = delegate (Person self, string name)
								{
									Assert.That(self, Is.SameAs(p));
									Assert.That(name, Is.EqualTo("ABC"));
								};
			p.Name = "ABC";
			
			Soldier s = new Soldier();
			s.Rank = Rank.Captain;
			Assert.That(s.Rank, Is.EqualTo(Rank.Captain));
			Soldier.StaticPrototype.get_Rank = delegate
									 {
										 return Rank.Lieutenant;
									 };
			Assert.That(s.Rank, Is.EqualTo(Rank.Lieutenant));

		}
	}
}
