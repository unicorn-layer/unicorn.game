
using System;
using Unicorn.Game.Internal;

namespace Unicorn.Game {
	/// <summary>
	/// Attribute to register a method as a server-side message endpoint on entity components.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class ServerAttribute : EndpointBaseAttribute {
		public ServerAttribute(byte code) : base(code) {
		}
	}
}
