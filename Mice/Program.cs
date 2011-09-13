using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;
using StrongNameKeyPair = System.Reflection.StrongNameKeyPair;
using System.IO;

namespace Mice
{
	static class Program
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
				foreach (var type in assembly.Modules.SelectMany(m => m.Types).Where(IsTypeToBeProcessed).ToArray())
				{
					ProcessType(type);
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
				//type.GenericParameters.Count == 0 &&
				!type.IsValueType && 
				!type.IsInterface &&
				type.BaseType.Name != "MulticastDelegate";
		}

		private static void Using()
		{
			Console.WriteLine("Usage: mice.exe assembly-name.dll [key-file.snk]");
		}


		private static void ProcessType(TypeDefinition type)
		{
			TypeDefinition prototypeType = CreatePrototypeType(type);

			FieldDefinition prototypeField = new FieldDefinition(/*type.Name +*/ "Prototype", FieldAttributes.Public, prototypeType);
			type.Fields.Add(prototypeField);

			FieldDefinition staticPrototypeField = new FieldDefinition("StaticPrototype", FieldAttributes.Public | FieldAttributes.Static, prototypeType);
			type.Fields.Add(staticPrototypeField);

			MethodDefinition[] methods = type.Methods.Where(IsMethodToBeProcessed).ToArray();

			
			Dictionary<string, int> name2Count = methods.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.Count());

			//create delegate types & fields, patch methods to call delegates
			foreach (var method in methods)
			{
				bool includeParamsToName = name2Count[method.Name] > 1;

				var delegateType = CreateDeligateType(method, prototypeType, includeParamsToName);
				var delegateField = CreateDeligateField(prototypeType, method, delegateType, includeParamsToName);

				MethodDefinition newMethod = MoveCodeToImplMethod(method);

				//AddStaticPrototypeCall(method, delegateField, staticPrototypeField);

				if (!method.IsStatic)
				{
					//AddInstancePrototypeCall(method, delegateField, prototypeField);
				}
			}

			//After using of Mice there always should be a wasy to create an instance of public class
			//Here we create methods that can call parameterless ctor, evern if there is no parameterless ctor :)
			if (!type.IsAbstract)
			{
				var privateDefaultCtor =
					type.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && !m.IsPublic && !m.IsStatic);

				if (privateDefaultCtor != null)
				{
					var delegateType = CreateDeligateType(privateDefaultCtor, prototypeType, false);
					var delegateField = CreateDeligateField(prototypeType, privateDefaultCtor, delegateType, false);

					MethodDefinition newMethod = MoveCodeToImplMethod(privateDefaultCtor);
					AddStaticPrototypeCall(privateDefaultCtor, delegateField, staticPrototypeField);

					CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
				}
				else
				{
					var publicDefaultCtor =
						type.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && m.IsPublic && !m.IsStatic);					
					if (publicDefaultCtor == null) //there is not default ctor, neither private nor public
					{
						privateDefaultCtor = CreateDefaultCtor(type);
						CreateCallToPrivateCtor(privateDefaultCtor, prototypeType);
					}
				}
			}
		}

		private static MethodDefinition CreateDefaultCtor(TypeDefinition type)
		{
			//create constructor
			var constructor = new MethodDefinition(".ctor",
				MethodAttributes.Private | MethodAttributes.CompilerControlled |
				MethodAttributes.RTSpecialName | MethodAttributes.SpecialName |
				MethodAttributes.HideBySig, type.Module.Import(typeof(void)));
			type.Methods.Add(constructor);
			constructor.Body.GetILProcessor().Emit(OpCodes.Ret);

			return constructor;
		}

		private static bool IsMethodToBeProcessed(MethodDefinition m)
		{
			return (m.IsPublic) && 
				//m.GenericParameters.Count == 0 &&
				!m.IsAbstract && 
				!(m.IsStatic && m.IsConstructor);
		}


		private static MethodDefinition CreateCallToPrivateCtor(MethodDefinition defCtor, TypeDefinition prototypeType)
		{
			MethodDefinition result = new MethodDefinition("CallCtor", MethodAttributes.Public, defCtor.DeclaringType);
			var il = result.Body.GetILProcessor();
			il.Emit(OpCodes.Newobj, defCtor);
			il.Emit(OpCodes.Ret);

			prototypeType.Methods.Add(result);

			return result;
		}

		private static MethodDefinition MoveCodeToImplMethod(MethodDefinition method)
		{
			const string realImplementationPrefix = "x";
			string name; 
			if (method.IsConstructor)
				name = realImplementationPrefix + "Ctor";
			else if (method.IsSetter && method.Name.StartsWith("set_"))
				name = "set_" + realImplementationPrefix + method.Name.Substring(4);
			else if (method.IsGetter && method.Name.StartsWith("get_"))
				name = "get_" + realImplementationPrefix + method.Name.Substring(4);
			else
				name = realImplementationPrefix + method.Name;

			MethodDefinition result = new MethodDefinition(name, method.Attributes, method.ReturnType);
			result.SemanticsAttributes = method.SemanticsAttributes;
			result.CallingConvention = method.CallingConvention;
			result.ExplicitThis = method.ExplicitThis;
			result.HasThis = method.HasThis;
			result.IsRuntimeSpecialName = false;
			result.IsVirtual = false;
			if (method.IsConstructor)
				result.IsSpecialName = false;

			result.Parameters.AddRange(method.Parameters.Select(Copy));
			result.Body.Variables.AddRange(method.Body.Variables.Select(Copy));
			result.GenericParameters.AddRange(method.GenericParameters.Select(p => p.Copy(result)));

			var il = result.Body.GetILProcessor();
			foreach (var inst in method.Body.Instructions)
			{
				Instruction newInst;
				if (inst.Operand == null)
					newInst = il.Create(inst.OpCode);
				else if (inst.Operand is TypeReference)
					newInst = il.Create(inst.OpCode, inst.Operand as TypeReference);
				else if (inst.Operand is CallSite)
					newInst = il.Create(inst.OpCode, inst.Operand as CallSite);
				else if (inst.Operand is MethodReference)
					newInst = il.Create(inst.OpCode, inst.Operand as MethodReference);
				else if (inst.Operand is FieldReference)
					newInst = il.Create(inst.OpCode, inst.Operand as FieldReference);
				else if (inst.Operand is string)
					newInst = il.Create(inst.OpCode, inst.Operand as string);
				else if (inst.Operand is sbyte)
					newInst = il.Create(inst.OpCode, (sbyte)inst.Operand);
				else if (inst.Operand is byte)
					newInst = il.Create(inst.OpCode, (byte)inst.Operand);
				else if (inst.Operand is int)
					newInst = il.Create(inst.OpCode, (int)inst.Operand);
				else if (inst.Operand is long)
					newInst = il.Create(inst.OpCode, (long)inst.Operand);
				else if (inst.Operand is float)
					newInst = il.Create(inst.OpCode, (float)inst.Operand);
				else if (inst.Operand is double)
					newInst = il.Create(inst.OpCode, (double)inst.Operand);
				else if (inst.Operand is Instruction)
					newInst = il.Create(inst.OpCode, (Instruction)inst.Operand);
				else if (inst.Operand is Instruction[])
					newInst = il.Create(inst.OpCode, (Instruction[])inst.Operand);
				else if (inst.Operand is VariableDefinition)
					newInst = il.Create(inst.OpCode, (VariableDefinition)inst.Operand);
				else if (inst.Operand is ParameterDefinition)
					newInst = il.Create(inst.OpCode, (ParameterDefinition)inst.Operand);
				else
					throw new NotSupportedException();

				il.Append(newInst);
			}

			foreach (var exHandler in method.Body.ExceptionHandlers)
			{
				result.Body.ExceptionHandlers.Add(new ExceptionHandler(exHandler.HandlerType)
				{
					CatchType = exHandler.CatchType,
					FilterStart = exHandler.FilterStart,
					HandlerEnd = exHandler.HandlerEnd,
					HandlerStart = exHandler.HandlerStart,
					TryEnd = exHandler.TryEnd,
					TryStart = exHandler.TryStart
				});
			}

			method.DeclaringType.Methods.Add(result);

			//registering a property if it's needed
			if (result.IsGetter || result.IsSetter)
			{
				TypeReference propertyType = result.IsGetter ? result.ReturnType : result.Parameters[0].ParameterType;
				string propertyName = result.Name.Substring(4);
				var property = method.DeclaringType.Properties.FirstOrDefault(p => p.Name == propertyName);
				if (property == null)
				{
					property = new PropertyDefinition(propertyName, PropertyAttributes.None, propertyType);
					method.DeclaringType.Properties.Add(property);
				}
				if (result.IsGetter)
					property.GetMethod = result;
				else
					property.SetMethod = result;
				
			}
			
			//repalce old method body
			method.Body.Instructions.Clear();
			method.Body.Variables.Clear();
			method.Body.ExceptionHandlers.Clear();
			
			il = method.Body.GetILProcessor();
			int allParamsCount = method.Parameters.Count + (method.IsStatic ? 0 : 1); //all params and maybe this
			for (int i = 0; i < allParamsCount; i++)
			    il.Emit(OpCodes.Ldarg, i);

			var methodToCall = result
				.MakeGeneric(result.DeclaringType.GenericParameters.ToArray())
				.MakeGenericMethod(method.GenericParameters.ToArray());

			il.Emit(OpCodes.Call, methodToCall);
			il.Emit(OpCodes.Ret);
			return result;
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

			return result;
		}

		private static FieldDefinition CreateDeligateField(TypeDefinition hostType, MethodDefinition method, TypeDefinition delegateType, bool includeParamsToName)
		{
			var fieldName = ComposeFullMethodName(method, includeParamsToName);

			FieldDefinition field = new FieldDefinition(fieldName, FieldAttributes.Public, delegateType);
			hostType.Fields.Add(field);
			return field;
		}

		private static TypeDefinition CreateDeligateType(MethodDefinition method, TypeDefinition parentType, bool includeParamsToName)
		{
			string deligateName = "Callback_" + ComposeFullMethodName(method, includeParamsToName);

			TypeReference multicastDeligateType = parentType.Module.Import(typeof(MulticastDelegate));
			TypeReference voidType = parentType.Module.Import(typeof(void));
			TypeReference objectType = parentType.Module.Import(typeof(object));
			TypeReference intPtrType = parentType.Module.Import(typeof(IntPtr));

			TypeDefinition result = new TypeDefinition(null, deligateName,
				TypeAttributes.Sealed | TypeAttributes.NestedPublic | TypeAttributes.RTSpecialName , multicastDeligateType);
			result.GenericParameters.AddRange(method.GenericParameters.Select(p => p.Copy(result)));


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
				var paramToAdd = param.Copy();
				if (paramToAdd.ParameterType.IsGenericParameter)
				{
					GenericParameter gpt = (GenericParameter)paramToAdd.ParameterType;
					
				}
			}
			
			result.Methods.Add(invoke); 

			result.DeclaringType = parentType;
			parentType.NestedTypes.Add(result);
			return result;
		}


		private static string ComposeFullMethodName(MethodDefinition method, bool includeParamsToName)
		{
			var @params = method.Parameters.Select(p =>
			{
				if (p.ParameterType.IsArray)
				{
					ArrayType array = (ArrayType)p.ParameterType;
					if (array.Dimensions.Count > 1)
						return array.ElementType.Name + "Array" + array.Dimensions.Count.ToString();
					else
						return array.ElementType.Name + "Array";
				}
				else
					return p.ParameterType.Name;
			});
			IEnumerable<string> partsOfName = new[] {method.IsConstructor ? "Ctor" : method.Name};
			if (includeParamsToName)
				partsOfName = partsOfName.Concat(@params);
			return string.Join("_", partsOfName.ToArray());
		}

		#region extensions

		private static ParameterDefinition Copy(this ParameterDefinition param)
		{
			return new ParameterDefinition(param.Name, param.Attributes, param.ParameterType);
		}

		private static VariableDefinition Copy(this VariableDefinition variable)
		{
			return new VariableDefinition(variable.Name, variable.VariableType);
		}

		private static GenericParameter Copy(this GenericParameter gParam, IGenericParameterProvider owner)
		{
			var result = new GenericParameter(gParam.Name, owner) { Attributes = gParam.Attributes };
			result.Constraints.AddRange(gParam.Constraints);
			result.CustomAttributes.AddRange(gParam.CustomAttributes);
			return result;
		}

		public static MethodReference MakeGenericMethod(this MethodReference self, params TypeReference[] arguments)
		{
			if (self.GenericParameters.Count == 0)
				return self;

			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException();

			var instance = new GenericInstanceMethod(self);
			instance.GenericArguments.AddRange(arguments);

			return instance;
		}

		public static MethodReference MakeGeneric(this MethodReference self, params TypeReference[] arguments)
		{
			var reference = new MethodReference(self.Name, self.ReturnType)
			{
				DeclaringType = self.DeclaringType.MakeGenericType(arguments),
				HasThis = self.HasThis,
				ExplicitThis = self.ExplicitThis,
				CallingConvention = self.CallingConvention,
			};

			reference.Parameters.AddRange(self.Parameters.Select(p => new ParameterDefinition(p.ParameterType)));

			reference.GenericParameters.AddRange(self.GenericParameters.Select(p => new GenericParameter(p.Name, reference)));

			return reference;
		}

		public static TypeReference MakeGenericType(this TypeReference self, params TypeReference[] arguments)
		{
			if (self.GenericParameters.Count == 0)
				return self;

			if (self.GenericParameters.Count != arguments.Length)
				throw new ArgumentException();

			var instance = new GenericInstanceType(self);
			instance.GenericArguments.AddRange(arguments);

			return instance;
		}

		public static void AddRange<T1, T2>(this Mono.Collections.Generic.Collection<T1> collection, IEnumerable<T2> items)
			where T2 : T1
		{
			foreach (var item in items)
				collection.Add(item);
		}
		#endregion
	}
}
