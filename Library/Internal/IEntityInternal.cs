
using Unicorn.IO;

namespace Unicorn.Game.Internal {
	public interface IEntityInternal {
		EntityId Id { get; set; }
		void Activate();
		void Deactivate();
		void Receive(Message msg);
		void SetOwnership(bool isMine);
	}
}
