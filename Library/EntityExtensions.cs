
using Unicorn.Game.Internal;
using Unicorn.IO;

namespace Unicorn.Game {
	public static class EntityExtensions {
		public static void Write(this DataWriter payload, Entity value) {
			if (value) {
				payload.Write(true);
				payload.Write(((IEntityInternal)value).Id);
			} else {
				payload.Write(false);
			}
		}

		public static Entity ReadEntity(this DataReader payload) {
			return payload.ReadBoolean() ? Entity.Find(payload.ReadEntityId()) : null;
		}
	}
}
