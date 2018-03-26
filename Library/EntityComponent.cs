
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

		/// <summary>
		/// Get the entity this component is attached to.
		/// </summary>
		/// <value></value>
		public Entity Entity {
			get {
				if (!_entity && Active)
					_entity = GetComponent<Entity>();
				return _entity;
			}
		}

		private byte _id;
		private bool _active;
		private bool _isTracked;
		private Disposable _untilDeactivate = new Disposable();
		private SortedDictionary<byte, MessageHandler> _endpoints;
		private static SortedDictionary<byte, MethodInfo> _serverEndpoints;
		private static SortedDictionary<byte, MethodInfo> _clientEndpoints;
		private static T _main;
		private static Set<T> _tracked = new Set<T>();

		/// <summary>
		/// True, if this entity has been activated. Do NOT use network api otherwise.
		/// </summary>
		/// <value></value>
		public bool Active { get { return _active; } }

		/// <summary>
		/// A disposable that will disposed when the entity is deactivated.
		/// </summary>
		/// <value></value>
		public Disposable UntilDeactivate { get { return _untilDeactivate; } }

		/// <summary>
		/// Get the instance that is marked as the main instance.
		/// </summary>
		/// <value></value>
		public static T Main { get { return _main; } }

		/// <summary>
		/// Get a set of tracked instances.
		/// </summary>
		/// <value></value>
		public static IReadonlyObservableSet<T> Tracked { get { return _tracked; } }

		/// <summary>
		/// Mark this instance as the main instance.
		/// </summary>
		/// <param name="replace"><c>true</c> to replace an existing instance.</param>
		protected bool SetMain(bool replace) {
			if (_main != this) {
				if (!replace && _main) {
					return false;
				}
				_main = (T)this;
			}
			return true;
		}

		/// <summary>
		/// Track this instance.
		/// </summary>
		protected bool Track() {
			_isTracked = true;
			return _tracked.Add((T)this);
		}

		/// <summary>
		/// Untrack this instance.
		/// </summary>
		protected bool Untrack() {
			_isTracked = false;
			return _tracked.Remove((T)this);
		}

		/// <summary>
		/// Send a network message to all available remote hosts.
		/// </summary>
		/// <param name="msg"></param>
		protected void Send(MessageWriter msg) {
			Send(0, msg);
		}

		/// <summary>
		/// Send a network message to all available remote hosts on a specific channel.
		/// </summary>
		/// <param name="channelKey"></param>
		/// <param name="msg"></param>
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

		/// <summary>
		/// Send a network message to the specified target.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="msg"></param>
		protected void Send(IEnumerable<Connection> target, MessageWriter msg) {
			Send(target, 0, msg);
		}

		/// <summary>
		/// Send a network message to the specified target on a specific channel.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="channelKey"></param>
		/// <param name="msg"></param>
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

		/// <summary>
		/// Called when the entity has been activated.
		/// </summary>
		protected virtual void OnEntityActivate() { }

		/// <summary>
		/// Called when the entity has deen deactivated.
		/// The default implementation disposes registered disposables.
		/// </summary>
		protected virtual void OnEntityDeactivate() {
			_untilDeactivate.Dispose();
			if (_isTracked) {
				Untrack();
			}
		}

		/// <summary>
		/// (Server only) Called when this entity is visible to a client.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityClientConnected(Connection conn) { }
		/// <summary>
		/// (Server only) Called when this entity is no longer visible to a client.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityClientDisconnected(Connection conn) { }

		/// <summary>
		/// (Server only) Called when an owner has been added.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityOwnerAdded(Connection conn) { }
		/// <summary>
		/// (Server only) Called when an owner has been added and the entity is visible to that owner.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityOwnerConnected(Connection conn) { }
		/// <summary>
		/// (Server only) Called when an owner has been removed.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityOwnerRemoved(Connection conn) { }
		/// <summary>
		/// (Server only) Called when an owner has been removed and the entity was visible to that owner.
		/// </summary>
		/// <param name="conn"></param>
		protected virtual void OnEntityOwnerDisconnected(Connection conn) { }

		/// <summary>
		/// (Client only) Called when the client connection has been added or removed from the server entity owner set.
		/// </summary>
		/// <param name="isMine">true, if added otherwise false.</param>
		protected virtual void OnEntityOwnershipChanged(bool isMine) { }
		


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

		void IEntityComponentInternal.OwnerAdded(Connection conn) {
			OnEntityOwnerAdded(conn);
		}

		void IEntityComponentInternal.OwnerConnected(Connection conn) {
			OnEntityOwnerConnected(conn);
		}

		void IEntityComponentInternal.OwnerRemoved(Connection conn) {
			OnEntityOwnerRemoved(conn);
		}

		void IEntityComponentInternal.OwnerDisconnected(Connection conn) {
			OnEntityOwnerDisconnected(conn);
		}

		void IEntityComponentInternal.OwnershipChanged(bool isMine) {
			OnEntityOwnershipChanged(isMine);
		}
	}
}
