
using Unicorn.Game.Internal;
using Unicorn.IO;

namespace Unicorn.Game {
	public static class EntityExtensions {
		public static void WriteRef(this DataWriter payload, Entity value) {
			if (value) {
				payload.Write(true);
				payload.Write(((IEntityInternal)value).Id);
			} else {
				payload.Write(false);
			}
		}

		public static void WriteRef<T>(this DataWriter payload, T component) where T : EntityComponent<T> {
			WriteRef(payload, component.Entity);
			payload.Write(((IEntityComponentInternal)component).Id);
		}

		public static Entity ReadEntityRef(this DataReader payload) {
			return payload.ReadBoolean() ? Entity.Find(payload.ReadEntityId()) : null;
		}

		public static T ReadEntityComponentRef<T>(this DataReader payload) where T : EntityComponent<T> {
			var entity = ReadEntityRef(payload);
			var componentId = payload.ReadByte();
			return entity ? ((IEntityInternal)entity).GetComponent<T>(componentId) : null;
		}
	}
}
