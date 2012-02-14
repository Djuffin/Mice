﻿using System;
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
				var field = new FieldDefinition(method.Name + ActionsPostfix, FieldAttributes.Assembly, method.Module.Import(ActionsType));
				declaringType.Fields.Add(field);
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
			actionSetter.GenericParameters.AddRange(method.GenericParameters.Select(p => p.Copy(actionSetter)));

			var actionParameterType = CreateActionSetterParameterType(method);
			ParameterDefinition actionParameter = new ParameterDefinition(actionParameterType);
			actionParameter.Name = ActionParameterName;

			actionSetter.Parameters.Add(actionParameter);

			WriteSetterILBody(actionSetter, actionsField);

			destinationType.Methods.Add(actionSetter);
		}

		public static GenericInstanceType CreateActionSetterParameterType(MethodDefinition originalMethod)
		{
			//there must be tuples here, but we don't have tuples in .NET 3.5
			Type[] tupleTypes = new[] { typeof(Func<>), typeof(Func<,>), typeof(Func<,,>), typeof(Func<,,,>), typeof(Func<,,,,>), typeof(Func<,,,,>) };

			int genParamsCount = originalMethod.Parameters.Count;
			if (!originalMethod.IsStatic)
				genParamsCount++; //self parameter

			if (genParamsCount > tupleTypes.Length)
				throw new InvalidOperationException("Mice does not support methods with more than" + tupleTypes.Length + "generic parameters");

			var origType = tupleTypes[genParamsCount - 1];
			
			var actionTypeReference = originalMethod.Module.Import(origType);

			var genericInstanceType = new GenericInstanceType(actionTypeReference);
			if (!originalMethod.IsStatic)
			{
				genericInstanceType.GenericArguments.Add(originalMethod.DeclaringType);	
			}
			genericInstanceType.GenericArguments.AddRange(originalMethod.Parameters.Select(p => p.ParameterType));
			if (originalMethod.ReturnType.FullName != originalMethod.Module.Import(typeof(void)).FullName)
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

		public static Collection<GenericParameter> CopyGenericParameters(Collection<GenericParameter> genericParameters, IGenericParameterProvider ownerType)
		{
			Collection<GenericParameter> result = new Collection<GenericParameter>();

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
			GenericInstanceType actionType = CreateActionSetterParameterType(method);

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
