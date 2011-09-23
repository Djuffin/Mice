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

		public const string GetActionPrefix = "get_";

		public const string ActionsPrefix = "Actions";

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
				var field = new FieldDefinition(method.Name + ActionsPrefix, FieldAttributes.Public, method.Module.Import(typeof(Dictionary<Type, object>)));
				declaringType.Fields.Add(field);
				GenericInstanceType declaringTypeForField = new GenericInstanceType(declaringType);
				declaringTypeForField.GenericArguments.AddRange(declaringType.GenericParameters);
				var instanceField = new FieldReference(field.Name, field.FieldType);
				instanceField.DeclaringType = declaringTypeForField;
				//declaringType.Fields[declaringType.Fields.IndexOf(field)] = instanceField.Resolve();
				return instanceField;
			}
			return null;
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

			var actionParameter = CreateActionSetterParameter(destinationType, method, actionSetter);

			actionSetter.Parameters.Add(actionParameter);

			WriteSetterILBody(actionSetter, actionsField);

			destinationType.Methods.Add(actionSetter);
		}

		public static ParameterDefinition CreateActionSetterParameter(TypeDefinition destinationType, MethodDefinition method, MethodDefinition resultMethod)
		{
			string typeName;
			int paramsCount = method.GenericParameters.Count;

			//self parameter
			if (!method.IsStatic)
			{
				paramsCount++;
			}

			if (method.ReturnType.Name != "Void")
			{
				paramsCount += 1;//1 for return type
				typeName = "System.Func`" + (paramsCount);
			}
			else
			{
				typeName = "System.Action`" + (paramsCount);
			}

			var winAssembly = System.Reflection.Assembly.Load("System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			System.Reflection.Assembly.GetExecutingAssembly().GetReferencedAssemblies();
			var origType = winAssembly.GetType(typeName);
			var assembly = AssemblyDefinition.ReadAssembly(winAssembly.Location);
			var module = assembly.Modules[0];
			var actionTypeReference = method.Module.Import(origType);
			actionTypeReference.GenericParameters.AddRange(resultMethod.GenericParameters);

			var genericInstanceType = new GenericInstanceType(actionTypeReference);

			if (!method.IsStatic)
			{
				var baseTypeInstance = new GenericInstanceType(resultMethod.DeclaringType.DeclaringType);
				baseTypeInstance.GenericArguments.AddRange(resultMethod.DeclaringType.GenericParameters);
				genericInstanceType.GenericArguments.Add(baseTypeInstance);
			}
			genericInstanceType.GenericArguments.AddRange(resultMethod.GenericParameters);

			if (method.ReturnType.Name != "Void")
			{
				genericInstanceType.GenericArguments.Add(method.ReturnType);
			}

			//add parameter to method
			ParameterDefinition actionParameter = new ParameterDefinition(genericInstanceType);
			actionParameter.Name = ActionParameterName;

			return actionParameter;
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
			//add local viriable - result of comparison
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

		public static void SetSelfParameter(this MethodReference targetMethod, MethodReference sourceMethod)
		{
			var selfParameter = new ParameterDefinition("self", ParameterAttributes.None, sourceMethod.DeclaringType);
			//instanciate generic type
			selfParameter.SetGenericInstanciating(sourceMethod.DeclaringType);
			targetMethod.Parameters.Add(selfParameter);
		}
	}
}
