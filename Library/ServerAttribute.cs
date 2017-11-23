
using System;
using Unicorn.Game.Internal;

namespace Unicorn.Game {
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class ServerAttribute : EndpointBaseAttribute {
		public ServerAttribute(byte code) : base(code) {
		}
	}
}
