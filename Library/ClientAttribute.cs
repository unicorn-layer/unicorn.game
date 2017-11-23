
using System;
using Unicorn.Game.Internal;

namespace Unicorn.Game {
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class ClientAttribute : EndpointBaseAttribute {
		public ClientAttribute(byte code) : base(code) {
		}
	}
}
