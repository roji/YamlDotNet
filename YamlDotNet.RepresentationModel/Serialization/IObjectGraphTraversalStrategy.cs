using System;

namespace YamlDotNet.RepresentationModel.Serialization
{
	/// <summary>
	/// Defines a strategy that walks through an object graph.
	/// </summary>
	public interface IObjectGraphTraversalStrategy
	{
		/// <summary>
		/// Traverses the specified object graph.
		/// </summary>
		/// <param name="graph">The graph.</param>
		/// <param name="visitor">An <see cref="IObjectGraphVisitor"/> that is to be notified during the traversal.</param>
		/// <param name="serializeAsType">An optional type override.
		/// If specified, <paramref name="graph"/>'s runtime type will be ignored and this will be used instead.</param>
		void Traverse(object graph, IObjectGraphVisitor visitor, Type serializeAsType);
	}
}