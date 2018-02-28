
using System;
using Unicorn.Game.Internal;

namespace Unicorn.Game {
	/// <summary>
	/// Attribute to register a method as a client-side message endpoint on entity components.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class ClientAttribute : EndpointBaseAttribute {
		public ClientAttribute(byte code) : base(code) {
		}
	}
}
