using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace Mice
{
	public static class Generics
	{
		public const string SetActionPrefix = "set_";

		public const string ActionsPostfix = "Actions";

		public const string ActionParameterName = "action";

		public static Type ActionsType = typeof(Dictionary<Type, object>);

		public static void InitializeMethod(TypeDefinition declaringType, MethodDefinition method)
		{
			FieldReference field = AddActionsField(declaringType, method);
			AddGenericActionSetter(declaringType, method, field);
		}

		private static FieldReference AddActionsField(TypeDefinition declaringType, MethodDefinition method)
		{
			if (method.HasGenericParameters)
			{
				var field = new FieldDefinition(method.Name + ActionsPostfix, FieldAttributes.Public, method.Module.Import(typeof(Dictionary<Type, object>)));
				declaringType.Fields.Add(field);
				GenericInstanceType declaringTypeForField = new GenericInstanceType(declaringType);
				return GetGenericInstanceField(field);
			}
			return null;
		}

		public static FieldReference GetGenericInstanceField(FieldDefinition field)
		{
			GenericInstanceType declaringTypeForField = new GenericInstanceType(field.DeclaringType);
			declaringTypeForField.GenericArguments.AddRange(field.DeclaringType.GenericParameters);
			var instanceField = new FieldReference(field.Name, field.FieldType);
			instanceField.DeclaringType = declaringTypeForField;
			return instanceField;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="destinationType">Type to wich add setter method</param>
		/// <param name="method">original method with generic parameters</param>
		private static void AddGenericActionSetter(TypeDefinition destinationType, MethodDefinition method, FieldReference actionsField)
		{
			//make method with generic parameters
			MethodDefinition actionSetter = new MethodDefinition(SetActionPrefix + method.Name, MethodAttributes.Public | MethodAttributes.HideBySig, method.Module.Import(typeof(void)));
			actionSetter.DeclaringType = destinationType;
			var copyGenericParameters = CopyGenericParameters(method.GenericParameters, actionSetter);

			actionSetter.GenericParameters.AddRange(copyGenericParameters);

			var actionParameterType = CreateActionSetterParameterType(method, actionSetter);
			ParameterDefinition actionParameter = new ParameterDefinition(actionParameterType);
			actionParameter.Name = ActionParameterName;

			actionSetter.Parameters.Add(actionParameter);

			WriteSetterILBody(actionSetter, actionsField);

			destinationType.Methods.Add(actionSetter);
		}

		public static GenericInstanceType CreateActionSetterParameterType(MethodDefinition originalMethod, MethodDefinition containingParameterMethod)
		{
			string typeName;
			int paramsCount = originalMethod.Parameters.Count;

			//self parameter
			if (!originalMethod.IsStatic)
			{
				paramsCount++;
			}

			if (originalMethod.ReturnType.Name != "Void")
			{
				paramsCount += 1;//1 for return type
				typeName = "System.Func`" + (paramsCount);
			}
			else
			{
				typeName = "System.Action`" + (paramsCount);
			}
			System.Reflection.Assembly winAssembly;

			if (typeName == "System.Action`1")
			{
				winAssembly = System.Reflection.Assembly.Load("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			}
			else 
			{
				winAssembly = System.Reflection.Assembly.Load("System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			}
			var origType = winAssembly.GetType(typeName);
			var assembly = AssemblyDefinition.ReadAssembly(winAssembly.Location);
			var module = assembly.Modules[0];
			var actionTypeReference = originalMethod.Module.Import(origType);
			actionTypeReference.GenericParameters.AddRange(containingParameterMethod.GenericParameters);

			var genericInstanceType = new GenericInstanceType(actionTypeReference);

			//add self parameter
			if (!originalMethod.IsStatic)
			{
				if (containingParameterMethod.DeclaringType.DeclaringType != null)
				{
					var baseTypeInstance = new GenericInstanceType(containingParameterMethod.DeclaringType.DeclaringType);
					baseTypeInstance.GenericArguments.AddRange(containingParameterMethod.DeclaringType.GenericParameters);
					genericInstanceType.GenericArguments.Add(baseTypeInstance);
				}
				else
				{
					var baseTypeInstance = new GenericInstanceType(containingParameterMethod.DeclaringType);
					baseTypeInstance.GenericArguments.AddRange(containingParameterMethod.DeclaringType.GenericParameters);
					genericInstanceType.GenericArguments.Add(baseTypeInstance);
				}
			}

			//add function parameters
			foreach (ParameterDefinition parameter in originalMethod.Parameters)
			{
				genericInstanceType.GenericArguments.Add(parameter.ParameterType);
			}

			//add return type parameter if presented
			if (originalMethod.ReturnType.Name != "Void")
			{
				genericInstanceType.GenericArguments.Add(originalMethod.ReturnType);
			}

			return genericInstanceType;
		}

		public static void WriteSetterILBody(MethodDefinition method, FieldReference actionsField)
		{
			ILProcessor ilProcessor = method.Body.GetILProcessor();
			
			//assignment
			Instruction firstInstr = ilProcessor.Create(OpCodes.Nop);
			Instruction assignInstr = ilProcessor.Create(OpCodes.Ldarg_0);
			ilProcessor.Append(firstInstr);
			ilProcessor.Append(assignInstr);
			ilProcessor.Emit(OpCodes.Ldfld, actionsField);
			ilProcessor.Emit(OpCodes.Ldtoken, method.Parameters[0].ParameterType);

			Type typeType = typeof(Type);
			System.Reflection.MethodInfo getTypeMethodInfo = typeType.GetMethod("GetTypeFromHandle");
			MethodReference getTypeMethod = method.Module.Import(getTypeMethodInfo);
			ilProcessor.Emit(OpCodes.Call, getTypeMethod);

			ilProcessor.Emit(OpCodes.Ldarg_1);

			System.Reflection.MethodInfo set_ItemMethodInfo = ActionsType.GetMethod("set_Item");
			MethodReference set_ItemMethod = method.Module.Import(set_ItemMethodInfo);
			ilProcessor.Emit(OpCodes.Callvirt, set_ItemMethod);

			ilProcessor.Emit(OpCodes.Nop);
			ilProcessor.Emit(OpCodes.Ret);

			System.Reflection.MemberInfo[] set_CtorMethodInfo = ActionsType.GetMember(".ctor");
			MethodReference set_CtorMethod = method.Module.Import((System.Reflection.MethodBase)set_CtorMethodInfo[0]);

			var instructions = new []
			{
				//initialize actionsField if it is null
				ilProcessor.Create(OpCodes.Nop),
				ilProcessor.Create(OpCodes.Ldarg_0),
				ilProcessor.Create(OpCodes.Ldfld, actionsField),
				ilProcessor.Create(OpCodes.Ldnull),
				ilProcessor.Create(OpCodes.Ceq),
				ilProcessor.Create(OpCodes.Ldc_I4_0),
				ilProcessor.Create(OpCodes.Ceq),
				ilProcessor.Create(OpCodes.Stloc_0),
				ilProcessor.Create(OpCodes.Ldloc_0),
				ilProcessor.Create(OpCodes.Brtrue_S, assignInstr),
				ilProcessor.Create(OpCodes.Nop),

				//instanciate actions fiels
				ilProcessor.Create(OpCodes.Ldarg_0),
				ilProcessor.Create(OpCodes.Newobj, set_CtorMethod),
				ilProcessor.Create(OpCodes.Stfld, actionsField),
			};

			foreach (var instr in instructions)
			{
				ilProcessor.InsertBefore(firstInstr, instr);
			}
			ilProcessor.Body.MaxStackSize = 3;
			ilProcessor.Body.InitLocals = true;
			VariableDefinition loc0 = new VariableDefinition(method.Module.Import(typeof(bool)));
			ilProcessor.Body.Variables.Add(loc0);
			ilProcessor.Body.SimplifyMacros();
			ilProcessor.Body.OptimizeMacros();
		}

		public static Mono.Collections.Generic.Collection<GenericParameter> CopyGenericParameters(Mono.Collections.Generic.Collection<GenericParameter> genericParameters, IGenericParameterProvider ownerType)
		{
			Mono.Collections.Generic.Collection<GenericParameter> result = new Mono.Collections.Generic.Collection<GenericParameter>();

			foreach (GenericParameter genericParameter in genericParameters)
			{
				GenericParameter copyGenericParameter = new GenericParameter(genericParameter.Name, ownerType);
				copyGenericParameter.Constraints.AddRange(genericParameter.Constraints);
				result.Add(copyGenericParameter);
			}

			return result;
		}

		public static void AddProrotypeCall(MethodDefinition method, FieldDefinition prototypeField)
		{
			GenericInstanceType actionType = CreateActionSetterParameterType(method, method);

			//initialize local variables
			var ilProcessor = method.Body.GetILProcessor();
			var firstInstruction = ilProcessor.Body.Instructions.First();

			if (prototypeField.IsStatic)
			{
				ilProcessor.Body.InitLocals = true;
				VariableDefinition loc0 = new VariableDefinition(actionType);
				ilProcessor.Body.Variables.Add(loc0);
				VariableDefinition loc1 = new VariableDefinition(method.Module.Import(typeof(bool)));
				ilProcessor.Body.Variables.Add(loc1);
			}

			var actionsFieldName = method.Name + ActionsPostfix;
			var actionsField = prototypeField.FieldType.Resolve().Fields.First(f => f.Name == actionsFieldName);
			var genericInstancePrototypeField = GetGenericInstanceField(prototypeField);
			var genericInstanceActionsField = GetGenericInstanceField(actionsField);

			Type typeType = typeof(Type);
			System.Reflection.MethodInfo getTypeMethodInfo = typeType.GetMethod("GetTypeFromHandle");
			MethodReference getTypeMethod = method.Module.Import(getTypeMethodInfo);

			System.Reflection.MethodInfo get_ItemMethodInfo = ActionsType.GetMethod("get_Item");
			MethodReference get_ItemMethod = method.Module.Import(get_ItemMethodInfo);

			System.Reflection.MethodInfo ContainsKeyMethodInfo = ActionsType.GetMethod("ContainsKey");
			MethodReference ContainsKeyMethod = method.Module.Import(ContainsKeyMethodInfo);

			MethodReference invokeAction = actionType.Resolve().Methods.First(m => m.Name == "Invoke");
			invokeAction = method.Module.Import(invokeAction);
			var genericInvokeAction = new GenericInstanceType(invokeAction.DeclaringType);
			genericInvokeAction.GenericArguments.AddRange(actionType.GenericArguments);
			invokeAction.DeclaringType = genericInvokeAction;

			var paramCount = method.Parameters.Count;
			
			if(!method.IsStatic)
			{
				paramCount++;
			}

			Instruction[] loadPrototypeFieldInstructions;
			if (prototypeField.IsStatic)
			{
				loadPrototypeFieldInstructions = new[] {
					ilProcessor.Create(OpCodes.Ldsflda, genericInstancePrototypeField)
				};
			}
			else
			{
				loadPrototypeFieldInstructions = new[] {
					ilProcessor.Create(OpCodes.Ldarg_0),
					ilProcessor.Create(OpCodes.Ldflda, genericInstancePrototypeField)
				};
			}

			var staticPrototypeCallInstructions = new[]
			{
				ilProcessor.Create(OpCodes.Nop),
			}.Concat(
				loadPrototypeFieldInstructions
			).Concat(new []{
				ilProcessor.Create(OpCodes.Ldfld, genericInstanceActionsField),
				ilProcessor.Create(OpCodes.Ldnull),
				ilProcessor.Create(OpCodes.Ceq),
				ilProcessor.Create(OpCodes.Stloc_1),
				ilProcessor.Create(OpCodes.Ldloc_1),
				ilProcessor.Create(OpCodes.Brtrue_S, firstInstruction),

				ilProcessor.Create(OpCodes.Nop),
			}).Concat(
				loadPrototypeFieldInstructions
			).Concat(new []{
				ilProcessor.Create(OpCodes.Ldfld, genericInstanceActionsField),
				ilProcessor.Create(OpCodes.Ldtoken, actionType),
				ilProcessor.Create(OpCodes.Call, getTypeMethod),
				ilProcessor.Create(OpCodes.Callvirt, ContainsKeyMethod),
				ilProcessor.Create(OpCodes.Ldc_I4_0),
				ilProcessor.Create(OpCodes.Ceq),
				ilProcessor.Create(OpCodes.Stloc_1),
				ilProcessor.Create(OpCodes.Ldloc_1),
				ilProcessor.Create(OpCodes.Brtrue_S, firstInstruction),

				ilProcessor.Create(OpCodes.Nop),
			}).Concat(
				loadPrototypeFieldInstructions
			).Concat(new []{
				ilProcessor.Create(OpCodes.Ldfld, genericInstanceActionsField),
				ilProcessor.Create(OpCodes.Ldtoken, actionType),
				ilProcessor.Create(OpCodes.Call, getTypeMethod),

				ilProcessor.Create(OpCodes.Callvirt, get_ItemMethod),
				ilProcessor.Create(OpCodes.Isinst, actionType),
				ilProcessor.Create(OpCodes.Stloc_0),
				ilProcessor.Create(OpCodes.Ldloc_0),
				ilProcessor.Create(OpCodes.Ldnull),
				ilProcessor.Create(OpCodes.Ceq),
				ilProcessor.Create(OpCodes.Stloc_1),
				ilProcessor.Create(OpCodes.Ldloc_1),
				ilProcessor.Create(OpCodes.Brtrue_S, firstInstruction),

				ilProcessor.Create(OpCodes.Nop),
				ilProcessor.Create(OpCodes.Ldloc_0),
			}).Concat(
				Enumerable.Range(0, paramCount).Select(i => ilProcessor.Create(OpCodes.Ldarg, i))
			)
			.Concat(new []{
				ilProcessor.Create(OpCodes.Callvirt, invokeAction),
				ilProcessor.Create(OpCodes.Nop),
				ilProcessor.Create(OpCodes.Ret),
			});

			foreach (var instruction in staticPrototypeCallInstructions)
			{
				ilProcessor.InsertBefore(firstInstruction, instruction);
			}
		}
	}
}
