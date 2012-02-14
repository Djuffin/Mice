using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mice
{
	internal static class Extensions
	{

		public static GenericParameter Copy(this GenericParameter gParam, IGenericParameterProvider owner)
		{
			var result = new GenericParameter(gParam.Name, owner) { Attributes = gParam.Attributes };
			result.Constraints.AddRange(gParam.Constraints);
			result.CustomAttributes.AddRange(gParam.CustomAttributes);
			return result;
		}

		public static ParameterDefinition Copy(this ParameterDefinition param)
		{
			return new ParameterDefinition(param.Name, param.Attributes, param.ParameterType);
		}

		public static VariableDefinition Copy(this VariableDefinition variable)
		{
			return new VariableDefinition(variable.Name, variable.VariableType);
		}

		public static MethodReference MakeGenericMethod(this MethodReference self, params TypeReference[] arguments)
		{
			if (!self.HasGenericParameters)
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

		public static FieldReference MakeGeneric(this FieldReference self, params TypeReference[] arguments)
		{
			var reference = new FieldReference(self.Name, self.FieldType)
			{
				DeclaringType = self.DeclaringType.MakeGenericType(arguments),
			};

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

		public static GenericInstanceType AsInstance(this TypeReference type)
		{
			var result = new GenericInstanceType(type);
			result.GenericArguments.AddRange(type.GenericParameters);
			return result;
		}

	}
}
