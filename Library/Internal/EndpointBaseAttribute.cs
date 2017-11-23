
using System;

namespace Unicorn.Game.Internal {
	public abstract class EndpointBaseAttribute : Attribute {
		public EndpointBaseAttribute(byte code) {
			_code = code;
		}

		private readonly byte _code;

		public byte Code { get { return _code; } }
	}
}
