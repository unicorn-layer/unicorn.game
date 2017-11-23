
using System;

namespace Unicorn.Game.Internal {
	[Serializable]
	public struct EntityId : IComparable<EntityId>, IEquatable<EntityId> {
		public EntityId(int value) {
			this.value = value;
		}
		
		public int value;

		public bool IsStatic { get { return value < 0; } }

		public int CompareTo(EntityId other) {
			return value.CompareTo(other.value);
		}

		public bool Equals(EntityId other) {
			return value == other.value;
		}

		public override bool Equals(object obj) {
			return obj is EntityId && ((EntityId)obj).value == value;
		}

		public override int GetHashCode() {
			unchecked { return value; }
		}

		public static bool operator ==(EntityId a, EntityId b) {
			return a.value == b.value;
		}

		public static bool operator !=(EntityId a, EntityId b) {
			return a.value != b.value;
		}

		public override string ToString() {
			return value == default(int)
				? "Unassigned"
				: (value < 0
					? string.Format("Static({0})", -value)
					: string.Format("Dynamic({0})", value));
		}
		
		public static readonly EntityId None = new EntityId(default(int));
	}
}
