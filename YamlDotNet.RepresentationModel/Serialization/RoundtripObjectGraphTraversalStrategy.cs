using System;
using System.Globalization;
using System.Linq;

namespace YamlDotNet.RepresentationModel.Serialization
{
	/// <summary>
	/// An implementation of <see cref="IObjectGraphTraversalStrategy"/> that traverses
	/// properties that are read/write, collections and dictionaries, while ensuring that
	/// the graph can be regenerated from the resulting document.
	/// </summary>
	public class RoundtripObjectGraphTraversalStrategy : FullObjectGraphTraversalStrategy
	{
		public RoundtripObjectGraphTraversalStrategy(Serializer serializer, ITypeDescriptor typeDescriptor, int maxRecursion)
			: base(serializer, typeDescriptor, maxRecursion)
		{
		}

		protected override void SerializeProperties(object value, Type type, Type staticType, IObjectGraphVisitor visitor, int currentDepth, Type serializeAsType)
		{
			if (!ReflectionUtility.HasDefaultConstructor(type) && !serializer.Converters.Any(c => c.Accepts(type)))
				throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type '{0}' cannot be deserialized because it does not have a default constructor or a type converter.", type));

			// Note that if SerializeAs is being used we do *not* emit a tag, since that tag would contain the SerializeAs type
			// which is not guaranteed to be assignable to the staticType (and therefore will break deserialization)
			visitor.VisitMappingStart(value, type, typeof(string), typeof(object), type != staticType && serializeAsType == null);

			foreach (var propertyDescriptor in typeDescriptor.GetProperties(type))
			{
				var propertyValue = propertyDescriptor.Property.GetValue(value, null);

				if (visitor.EnterMapping(propertyDescriptor, propertyValue))
				{
					Traverse(propertyDescriptor.Name, typeof(string), visitor, currentDepth);
					var attr = propertyDescriptor.Property.GetCustomAttributes(typeof(YamlMemberAttribute), true).Cast<YamlMemberAttribute>().FirstOrDefault();
					if (attr != null && attr.SerializeAs != null)
						Traverse(propertyValue, propertyDescriptor.Property.PropertyType, visitor, currentDepth, attr.SerializeAs);
					else
						Traverse(propertyValue, propertyDescriptor.Property.PropertyType, visitor, currentDepth);
				}
			}

			visitor.VisitMappingEnd(value, type);
		}
	}
}