
using System;
using System.Collections.Generic;
using System.Reflection;
using Unicorn.Game.Internal;
using Unicorn.IO;
using Unicorn.Util;
using UnityEngine;

namespace Unicorn.Game {
	[RequireComponent(typeof(Entity))]
	public class EntityComponent<T> : MonoBehaviour, IEntityComponentInternal where T : EntityComponent<T> {
		private Entity _entity;
		public Entity Entity {
			get {
				if (!_entity && Active)
					_entity = GetComponent<Entity>();
				return _entity;
			}
		}

		private bool _active;
		public bool Active { get { return _active; } }

		private Disposable _untilDeactivate = new Disposable();
		public Disposable UntilDeactivate { get { return _untilDeactivate; } }

		private byte _id;
		private SortedDictionary<byte, MessageHandler> _endpoints;

		private static SortedDictionary<byte, MethodInfo> _serverEndpoints;
		private static SortedDictionary<byte, MethodInfo> _clientEndpoints;
		


		protected void Send(MessageWriter msg) {
			Send(0, msg);
		}

		protected void Send(int channelKey, MessageWriter msg) {
			var router = EntityRouter.Require();
			if (router.IsServer) {
				if (Active) {
					Send(Entity.Group, channelKey, msg);
				} else {
					EntityInactiveSendWarning();
				}
			} else {
				Send(router.Connections, channelKey, msg);
			}
		}
		
		protected void Send(IEnumerable<Connection> target, MessageWriter msg) {
			Send(target, 0, msg);
		}
		
		protected void Send(IEnumerable<Connection> target, int channelKey, MessageWriter msg) {
			if (Active) {
				target.Send(channelKey, payload => {
					payload.Write(((IEntityInternal)Entity).Id);
					payload.Write(_id);
					msg(payload);
				});
			} else {
				EntityInactiveSendWarning();
			}
		}

		private void EntityInactiveSendWarning() {
			Debug.LogWarning("Trying to send network message although the entity is not active.");
		}
		
		protected void Endpoint(DataWriter payload, byte code) {
			payload.Write(code);
		}

		protected virtual void OnEntityActivate() { }

		protected virtual void OnEntityDeactivate() {
			_untilDeactivate.Dispose();
		}

		protected virtual void OnEntityClientConnected(Connection conn) { }
		protected virtual void OnEntityClientDisconnected(Connection conn) { }
		
		

		private void RegisterEndpoints<EndpointAttribute>(ref SortedDictionary<byte, MethodInfo> cache)
			where EndpointAttribute : EndpointBaseAttribute {

			if (cache == null) {
				cache = new SortedDictionary<byte, MethodInfo>();
				var flags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic;
				for (var type = GetType(); type != null; type = type.BaseType) {
					foreach (var method in type.GetMethods(flags)) {
						var attrs = method.GetCustomAttributes(typeof(EndpointAttribute), true);
						if (attrs.Length > 0) {
							cache.Add(((EndpointAttribute)attrs[0]).Code, method);
						}
					}
				}
			}

			_endpoints = new SortedDictionary<byte, MessageHandler>();
			foreach(var pair in cache) {
				_endpoints.Add(pair.Key, (MessageHandler)Delegate.CreateDelegate(typeof(MessageHandler), this, pair.Value));
			}
		}

		void IEntityComponentInternal.Activate(byte componentId) {
			_active = true;
			_id = componentId;

			if (EntityRouter.Main.IsServer) {
				RegisterEndpoints<ServerAttribute>(ref _serverEndpoints);
			} else {
				RegisterEndpoints<ClientAttribute>(ref _clientEndpoints);
			}

			OnEntityActivate();
		}

		void IEntityComponentInternal.Deactivate() {
			try {
				OnEntityDeactivate();
			} finally {
				_active = false;
				_id = 0;
				_endpoints = null;
			}
		}

		void IEntityComponentInternal.Receive(Message msg) {
			if (_active) {
				var endpointCode = msg.ReadByte();
				MessageHandler endpoint;
				if (_endpoints.TryGetValue(endpointCode, out endpoint)) {
					endpoint(msg);
				} else {
					Debug.LogErrorFormat("Unknown endpoint code or wrong peer state: {0}", endpointCode);
				}
			}
		}

		void IEntityComponentInternal.Connected(Connection conn) {
			OnEntityClientConnected(conn);
		}

		void IEntityComponentInternal.Disconnected(Connection conn) {
			OnEntityClientDisconnected(conn);
		}
	}
}
