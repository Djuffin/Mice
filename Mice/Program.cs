using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Mice
{
	class Program
	{
		const string victimName = "Cheese.dll";

		static void Main(string[] args)
		{
			
			var assembly = AssemblyDefinition.ReadAssembly(victimName);

			foreach (var type in assembly.Modules.SelectMany(m => m.Types).ToArray())
			{
				Console.WriteLine(type.FullName);
				if (type.IsPublic)
				{
					CreateStaticPrototype(type);
				}
				foreach (var method in type.Methods)
				{
					Console.WriteLine(method.FullName);
				}
			}

			assembly.Write(victimName);
		}

		private static TypeDefinition CreateStaticPrototype(TypeDefinition type)
		{
			TypeDefinition result = new TypeDefinition(type.Namespace, type.Name + "Prototype", TypeAttributes.Public | TypeAttributes.Sealed);
			type.Module.Types.Add(result);
			
			return result;
		}
	}
}
