using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mice
{
	class Program
	{

		static int Main(string[] args)
		{
		
			if (args.Length != 1)
			{
				Using();
				return 1;
			}

			string victimName = args[0];

			var assembly = AssemblyDefinition.ReadAssembly(victimName);
			foreach (var type in assembly.Modules.SelectMany(m => m.Types).ToArray())
			{
				if (type.IsPublic)
				{
					CreatePrototypeType(type);
				}
				foreach (var method in type.Methods)
				{
					
					//CallStub(method);
					
				}
			}

			assembly.Write(victimName);
			return 0;

			
		}

		private static void Using()
		{
			Console.WriteLine("Usage: mice.exe assembly-name.dll");
		}

		private static void CallStub(MethodDefinition method)
		{
			var firstInstruction = method.Body.Instructions.First();
			var il = method.Body.GetILProcessor();
			il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4, 1));
			il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "cat"));
			il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "CALL:" + method.FullName));

			var call = il.Create(OpCodes.Call,
				method.Module.Import(
					typeof(System.Diagnostics.Debugger).GetMethod("Log", new[] { typeof(int), typeof(string), typeof(string) })));

			il.InsertBefore(firstInstruction, call);
			Console.WriteLine(method.FullName);

		}



		private static TypeDefinition CreatePrototypeType(TypeDefinition type)
		{
			TypeDefinition result = new TypeDefinition(type.Namespace, "PrototypeClass", 
				TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.NestedPublic, type.Module.Import(typeof(object)));
			type.NestedTypes.Add(result);
			result.DeclaringType = type;
			type.Module.Types.Add(result);

			foreach (var method in type.Methods.Where(m => m.IsPublic))
			{
				CreateDeligateType(method, result);
			}

			return result;
		}

		private static TypeDefinition CreateDeligateType(MethodDefinition method, TypeDefinition parentType)
		{
			string paramsPostfix = string.Join("_", method.Parameters.Select(p => p.ParameterType.Name).ToArray());
			string deligateName = "Callback_" + method.Name + paramsPostfix;

			TypeReference multicastDeligateType = parentType.Module.Import(typeof(MulticastDelegate));
			TypeReference voidType = parentType.Module.Import(typeof(void));
			TypeReference objectType = parentType.Module.Import(typeof(object));
			TypeReference intPtrType = parentType.Module.Import(typeof(IntPtr));
			TypeReference asyncResultType = parentType.Module.Import(typeof(IAsyncResult));
			TypeReference asyncCallbackType = parentType.Module.Import(typeof(AsyncCallback));

			TypeDefinition result = new TypeDefinition(parentType.Namespace, deligateName,
				TypeAttributes.Public | TypeAttributes.Sealed /*| TypeAttributes.NestedPublic*/, multicastDeligateType);

			//create constructor
			var constructor = new MethodDefinition(".ctor",
				MethodAttributes.Public | MethodAttributes.CompilerControlled |
				MethodAttributes.RTSpecialName | MethodAttributes.SpecialName |
				MethodAttributes.HideBySig, voidType);
			constructor.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, objectType));
			constructor.Parameters.Add(new ParameterDefinition("method", ParameterAttributes.None, intPtrType));
			constructor.IsRuntime = true;
			result.Methods.Add(constructor);


			//create Invoke
			var invoke = new MethodDefinition("Invoke", method.Attributes, method.ReturnType);
			invoke.IsRuntime = true;
			foreach (var param in method.Parameters)
			{
				invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
			}
			result.Methods.Add(invoke); 

			//create BeginInvoke
			var begininvoke = new MethodDefinition("BeginInvoke", method.Attributes, asyncResultType);
			begininvoke.IsRuntime = true;
			foreach (var param in method.Parameters)
			{
				begininvoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
			}
			begininvoke.Parameters.Add(new ParameterDefinition("callback", ParameterAttributes.None, asyncCallbackType));
			begininvoke.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, objectType));
			result.Methods.Add(begininvoke);

			//create EndInvoke
			var endinvoke = new MethodDefinition("EndInvoke", method.Attributes, method.ReturnType);
			endinvoke.IsRuntime = true;
			endinvoke.Parameters.Add(new ParameterDefinition("result", ParameterAttributes.None, asyncResultType));
			result.Methods.Add(endinvoke);

			//result.DeclaringType = parentType;
			//parentType.NestedTypes.Add(result);
			parentType.Module.Types.Add(result);
			return result;
		}
	}
}
