using System;
using System.Linq.Expressions;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

// nicked from GraphProcessor project
namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram
{
	public static class TypeExtension
	{
		public static bool IsReallyAssignableFrom(this Type type, Type otherType)
		{
			if (type == null && otherType != null) return false;
			if (otherType == null && type != null) return false;

			if (type == otherType)
				return true;
			if (type.IsAssignableFrom(otherType))
				return true;
			if (otherType.IsAssignableFrom(type))
				return true;
			if (type == typeof(IUdonEventReceiver) && otherType == typeof(Component))
				return true;

			try
			{
				var v = Expression.Variable(otherType);
				var expr = Expression.Convert(v, type);
				return expr.Method != null && expr.Method.Name != "op_Implicit";
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

	}
}