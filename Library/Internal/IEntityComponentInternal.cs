
using Unicorn.IO;

namespace Unicorn.Game.Internal {
	public interface IEntityComponentInternal {
		byte Id { get; }
		void Activate(byte componentId);
		void Deactivate();
		void Receive(Message msg);
		void Connected(Connection conn);
		void Disconnected(Connection conn);
		void OwnerAdded(Connection conn);
		void OwnerConnected(Connection conn);
		void OwnerRemoved(Connection conn);
		void OwnerDisconnected(Connection conn);
		void OwnershipChanged(bool isMine);
	}
}
