
using System;

namespace Unicorn.Game {
	public class InvalidEntityComponentException : Exception {
		public InvalidEntityComponentException(Type instanceType, Type componentType)
			: base(string.Format("The type {0} must inherit from {1}.", instanceType.Name, componentType.Name)) {
		}
	}
}