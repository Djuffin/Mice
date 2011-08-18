using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;
using StrongNameKeyPair = System.Reflection.StrongNameKeyPair;
using System.IO;

namespace Mice
{
	class Program
	{

		static int Main(string[] args)
		{
			if (args.Length < 1)
			{
				Using();
				return 1;
			}

			string victimName = args[0];
			string keyFile = args.Length > 1 ? args[1] : null;

			try
			{
				var assembly = AssemblyDefinition.ReadAssembly(victimName);
				foreach (var type in assembly.Modules.SelectMany(m => m.Types).ToArray())
				{
					if (IsTypeToBeProcessed(type))
					{
						ProcessType(type);
					}
				}

				var writerParams = new WriterParameters();
				if (!string.IsNullOrEmpty(keyFile) && File.Exists(keyFile))
				{
					writerParams.StrongNameKeyPair = new StrongNameKeyPair(File.ReadAllBytes(keyFile));
				}

				assembly.Write(victimName, writerParams);
				return 0;
			}
			catch (Exception e)
			{
				Console.WriteLine("Error. " + e.ToString());
				return 1;
			}
		}

		private static bool IsTypeToBeProcessed(TypeDefinition type)
		{
			return type.IsPublic && 
				!type.IsEnum && 
				!type.IsValueType && 
				type.BaseType.Name != "MulticastDelegate";
		}

		private static void Using()
		{
			Console.WriteLine("Usage: mice.exe assembly-name.dll [key-file.snk]");
		}


		private static void ProcessType(TypeDefinition type)
		{
			TypeDefinition prototypeType = CreatePrototypeType(type);

			FieldDefinition prototypeField = new FieldDefinition(type.Name + "Prototype", FieldAttributes.Public, prototypeType);
			type.Fields.Add(prototypeField);

			FieldDefinition staticPrototypeField = new FieldDefinition("StaticPrototype", FieldAttributes.Public | FieldAttributes.Static, prototypeType);
			type.Fields.Add(staticPrototypeField);

			//create delegate types & fields, patch methods to call delegates
			foreach (var method in type.Methods.Where(m => m.IsPublic && !m.IsAbstract))
			{
				var delegateType = CreateDeligateType(method, prototypeType);
				var delegateField = CreateDeligateField(prototypeType, method, delegateType);
				AddStaticPrototypeCall(method, delegateField, staticPrototypeField);

				if (!method.IsStatic)
				{
					AddInstancePrototypeCall(method, delegateField, prototypeField);
				}
			}
		}

		private static void AddStaticPrototypeCall(MethodDefinition method, FieldDefinition delegateField, FieldDefinition prototypeField)
		{
			Debug.Assert(prototypeField.IsStatic);
			var firstOpcode = method.Body.Instructions.First();
			var il = method.Body.GetILProcessor();

			TypeDefinition delegateType = delegateField.FieldType.Resolve();
			var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
			int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this

			var instructions = new[]
			{
				il.Create(OpCodes.Ldsflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldsflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
			}.Concat(
				Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
			).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod),
				il.Create(OpCodes.Ret),
			});

			foreach (var instruction in instructions)
				il.InsertBefore(firstOpcode, instruction);
		}

		private static void AddInstancePrototypeCall(MethodDefinition method, FieldDefinition delegateField, FieldDefinition prototypeField)
		{
			Debug.Assert(!prototypeField.IsStatic);
			var firstOpcode = method.Body.Instructions.First();
			var il = method.Body.GetILProcessor();

			TypeDefinition  delegateType = delegateField.FieldType.Resolve();
			var invokeMethod = delegateType.Methods.Single(m => m.Name == "Invoke");
			int allParamsCount = method.Parameters.Count + 1; //all params and this

			var instructions = new[]
			{
				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
				il.Create(OpCodes.Brfalse, firstOpcode),

				il.Create(OpCodes.Ldarg_0),
				il.Create(OpCodes.Ldflda, prototypeField),
				il.Create(OpCodes.Ldfld, delegateField),
			}.Concat(
				Enumerable.Range(0, allParamsCount).Select(i => il.Create(OpCodes.Ldarg, i))
			).Concat(new[]
			{
				il.Create(OpCodes.Callvirt, invokeMethod),
				il.Create(OpCodes.Ret),
			});

			foreach (var instruction in instructions)
				il.InsertBefore(firstOpcode, instruction);
		}

		private static TypeDefinition CreatePrototypeType(TypeDefinition type)
		{
			TypeDefinition result = new TypeDefinition(null, "PrototypeClass", 
				TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout, 
				type.Module.Import(typeof(ValueType)));
			type.NestedTypes.Add(result);
			result.DeclaringType = type;

			//create .ctor
			var constructor = new MethodDefinition(".ctor", MethodAttributes.Public  
				| MethodAttributes.RTSpecialName | MethodAttributes.SpecialName |MethodAttributes.HideBySig,
				result.Module.Import(typeof(void)));
			var ctorIl = constructor.Body.GetILProcessor();
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Call, type.Module.Import(typeof(object).GetConstructor(new Type[0])));
			ctorIl.Emit(OpCodes.Ret);
			result.Methods.Add(constructor);

			return result;
		}

		private static FieldDefinition CreateDeligateField(TypeDefinition hostType, MethodDefinition method, TypeDefinition delegateType)
		{
			string paramsPostfix = string.Join("_", method.Parameters.Select(p => p.ParameterType.Name).ToArray());
			string fieldName = (method.IsConstructor ? "Ctor" : method.Name) + paramsPostfix;

			FieldDefinition field = new FieldDefinition(fieldName, FieldAttributes.Public, delegateType);
			hostType.Fields.Add(field);
			return field;
		}

		private static TypeDefinition CreateDeligateType(MethodDefinition method, TypeDefinition parentType)
		{
			string paramsPostfix = string.Join("_", method.Parameters.Select(p => p.ParameterType.Name).ToArray());
			string deligateName = "Callback_" + 
				(method.IsConstructor ? "Ctor" : method.Name) + paramsPostfix;

			TypeReference multicastDeligateType = parentType.Module.Import(typeof(MulticastDelegate));
			TypeReference voidType = parentType.Module.Import(typeof(void));
			TypeReference objectType = parentType.Module.Import(typeof(object));
			TypeReference intPtrType = parentType.Module.Import(typeof(IntPtr));

			TypeDefinition result = new TypeDefinition(null, deligateName,
				TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.RTSpecialName , multicastDeligateType);

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
			var invoke = new MethodDefinition("Invoke", 
				MethodAttributes.Public | MethodAttributes.HideBySig |
				MethodAttributes.NewSlot | MethodAttributes.Virtual, method.ReturnType);
			invoke.IsRuntime = true;
			if (!method.IsStatic)
			{
				invoke.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, method.DeclaringType));
			}
			foreach (var param in method.Parameters)
			{
				invoke.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
			}
			result.Methods.Add(invoke); 

			result.DeclaringType = parentType;
			parentType.NestedTypes.Add(result);
			return result;
		}
	}
}
