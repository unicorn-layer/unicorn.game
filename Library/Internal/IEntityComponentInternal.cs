
using Unicorn.IO;

namespace Unicorn.Game.Internal {
	public interface IEntityComponentInternal {
		void Activate(byte componentId);
		void Deactivate();
		void Receive(Message msg);
		void Connected(Connection conn);
		void Disconnected(Connection conn);
	}
}
