
using Unicorn.IO;

namespace Unicorn.Game.Internal {
	public static class EntityIds {
		private static readonly int _first = default(int) + 1;
		private static int _nextId = _first;

		public static void Reset() {
			_nextId = _first;
		}

		public static EntityId Allocate() {
			checked { return new EntityId(_nextId++); }
		}
		
		public static EntityId ReadEntityId(this DataReader payload) {
			return new EntityId(payload.ReadInt32());
		}

		public static void Write(this DataWriter payload, EntityId value) {
			payload.Write(value.value);
		}
	}
}
