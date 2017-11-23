
using System;
using Unicorn.Game.Internal;
using Unicorn.Util;

namespace Unicorn.Game {
	public class EntityNetwork : GlobalRouterBase<EntityRouter> {
		public static void Init(RouterConfig config, string managerResourcePath) {
			Initialize(new EntityRouter(config, managerResourcePath));
		}

		public static IReadonlyObservableSet<Connection> Scene {
			get { return EntityRouter.Require().Scene; }
		}
	}
}
