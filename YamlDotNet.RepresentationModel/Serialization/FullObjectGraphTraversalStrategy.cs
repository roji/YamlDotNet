using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace YamlDotNet.RepresentationModel.Serialization
{
	/// <summary>
	/// An implementation of <see cref="IObjectGraphTraversalStrategy"/> that traverses
	/// readable properties, collections and dictionaries.
	/// </summary>
	public class FullObjectGraphTraversalStrategy : IObjectGraphTraversalStrategy
	{
		protected readonly Serializer serializer;
		private readonly int maxRecursion;
		private readonly ITypeDescriptor typeDescriptor;

		public FullObjectGraphTraversalStrategy(Serializer serializer, ITypeDescriptor typeDescriptor, int maxRecursion)
		{
			if (maxRecursion <= 0)
			{
				throw new ArgumentOutOfRangeException("maxRecursion", maxRecursion, "maxRecursion must be greater than 1");
			}

			this.serializer = serializer;

			if (typeDescriptor == null)
			{
				throw new ArgumentNullException("typeDescriptor");
			}

			this.typeDescriptor = typeDescriptor;

			this.maxRecursion = maxRecursion;
		}

		void IObjectGraphTraversalStrategy.Traverse(object graph, IObjectGraphVisitor visitor)
		{
			Traverse(graph, visitor, 0);
		}

		void IObjectGraphTraversalStrategy.Traverse(object graph, IObjectGraphVisitor visitor, Type typeOverride)
		{
			Traverse(graph, visitor, 0, typeOverride);
		}

		protected virtual void Traverse(object value, IObjectGraphVisitor visitor, int currentDepth, Type typeOverride=null)
		{
			if (value == null)
			{
				visitor.VisitScalar(null, null);
				return;
			}

			var type = typeOverride ?? value.GetType();

			if (++currentDepth > maxRecursion)
			{
				throw new InvalidOperationException("Too much recursion when traversing the object graph");
			}

			if (!visitor.Enter(value, type))
			{
				return;
			}

			var typeCode = Type.GetTypeCode(type);
			switch (typeCode)
			{
				case TypeCode.Boolean:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
				case TypeCode.String:
				case TypeCode.Char:
				case TypeCode.DateTime:
					visitor.VisitScalar(value, type);
					break;

				case TypeCode.DBNull:
					visitor.VisitScalar(null, type);
					break;

				case TypeCode.Empty:
					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "TypeCode.{0} is not supported.", typeCode));

				default:
					if (type == typeof(TimeSpan))
					{
						visitor.VisitScalar(value, type);
						break;
					}

					TraverseObject(value, type, visitor, currentDepth);
					break;
			}
		}

		protected virtual void TraverseObject(object value, Type type, IObjectGraphVisitor visitor, int currentDepth)
		{
			var dict = value as IDictionary;
			if (dict != null)
			{
				TraverseDictionary(dict, type, visitor, currentDepth);
				return;
			}

			var dictionaryType = ReflectionUtility.GetImplementedGenericInterface(type, typeof(IDictionary<,>));
			if (dictionaryType != null)
			{
				TraverseGenericDictionary(value, type, dictionaryType, visitor);
				return;
			}

			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				SerializeList(value, type, visitor, currentDepth);
				return;
			}

			SerializeProperties(value, type, visitor, currentDepth);
		}

		protected virtual void TraverseDictionary(IDictionary value, Type type, IObjectGraphVisitor visitor, int currentDepth)
		{
			visitor.VisitMappingStart(value, type, typeof(object), typeof(object));

			foreach (DictionaryEntry entry in (IDictionary)value)
			{
				var keyType = GetObjectType(entry.Key);
				var valueType = GetObjectType(entry.Value);
				if (visitor.EnterMapping(entry.Key, keyType, entry.Value, valueType))
				{
					Traverse(entry.Key, visitor, currentDepth);
					Traverse(entry.Value, visitor, currentDepth);
				}
			}

			visitor.VisitMappingEnd(value, type);
		}

		private void TraverseGenericDictionary(object value, Type type, Type dictionaryType, IObjectGraphVisitor visitor)
		{
			var entryTypes = dictionaryType.GetGenericArguments();

			// dictionaryType is IDictionary<TKey, TValue>
			visitor.VisitMappingStart(value, type, entryTypes[0], entryTypes[1]);

			// Invoke TraverseGenericDictionaryHelper<,>
			traverseGenericDictionaryHelperGeneric
				.MakeGenericMethod(entryTypes)
				.Invoke(null, new object[] { this, value, visitor });

			visitor.VisitMappingEnd(value, type);
		}

		private static readonly MethodInfo traverseGenericDictionaryHelperGeneric =
			ReflectionUtility.GetMethod((FullObjectGraphTraversalStrategy s) => s.TraverseGenericDictionaryHelper<int, int>(null, null, 0));

		private void TraverseGenericDictionaryHelper<TKey, TValue>(
			IDictionary<TKey, TValue> value,
			IObjectGraphVisitor visitor, int currentDepth)
		{
			foreach (var entry in value)
			{
				if (visitor.EnterMapping(entry.Key, typeof(TKey), entry.Value, typeof(TValue)))
				{
					Traverse(entry.Key, visitor, currentDepth);
					Traverse(entry.Value, visitor, currentDepth);
				}
			}
		}

		private void SerializeList(object value, Type type, IObjectGraphVisitor visitor, int currentDepth)
		{
			var enumerableType = ReflectionUtility.GetImplementedGenericInterface(type, typeof(IEnumerable<>));
			var itemType = enumerableType != null ? enumerableType.GetGenericArguments()[0] : typeof(object);

			visitor.VisitSequenceStart(value, type, itemType);

			foreach (var item in (IEnumerable)value)
			{
				// TODO: Add SerializeMembersAs for list *elements*?
				Traverse(item, visitor, currentDepth);
			}

			visitor.VisitSequenceEnd(value, type);
		}

		protected virtual void SerializeProperties(object value, Type type, IObjectGraphVisitor visitor, int currentDepth)
		{
			visitor.VisitMappingStart(value, type, typeof(string), typeof(object));

			foreach (var propertyDescriptor in typeDescriptor.GetProperties(type))
			{
				var propertyValue = propertyDescriptor.Property.GetValue(value, null);

				if (visitor.EnterMapping(propertyDescriptor, propertyValue))
				{
					Traverse(propertyDescriptor.Name, visitor, currentDepth);
					// TODO: Add SerializeAs
					Traverse(propertyValue, visitor, currentDepth);
				}
			}

			visitor.VisitMappingEnd(value, type);
		}

		private static Type GetObjectType(object value)
		{
			return value != null ? value.GetType() : typeof(object);
		}
	}
}