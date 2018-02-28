
using System;
using System.Collections.Generic;
using Unicorn.Game.Internal;
using Unicorn.IO;
using Unicorn.Util;
using UnityEngine;

namespace Unicorn.Game {
	/// <summary>
	/// A network entity that can communicate with matching instances on remote hosts.
	/// </summary>
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public sealed class Entity : MonoBehaviour, IEntityInternal {
		[SerializeField]
		private EntityId _id = EntityId.None;
		private string _resourcePath;
		private bool _active;
		private Disposable _untilDeactivate;
		private SetProxy<Connection> _group;
		private SetProxy<Connection> _owners;
		private SubSet<Connection> _ownerSet;
		private SortedDictionary<byte, IEntityComponentInternal> _components = new SortedDictionary<byte, IEntityComponentInternal>();
		private byte _nextComponentId;
		private bool _isMine;

		private static SortedDictionary<EntityId, Entity> _map = new SortedDictionary<EntityId, Entity>();
		private static SortedDictionary<string, GameObject> _resources = new SortedDictionary<string, GameObject>();



		/// <summary>
		/// (Server only) Set of clients to which this entity is visible.
		/// </summary>
		/// <value></value>
		public IReadonlyObservableSet<Connection> Group {
			get {
				if (_group == null)
					throw new InvalidOperationException("Group can only be used on active server entities.");
				return _group;
			}
			set {
				if (_group == null)
					throw new InvalidOperationException("Group can only be used on active server entities.");
				if (_id.IsStatic)
					throw new InvalidOperationException("Group cannot be set for static entities.");
				_group.Target = value;
			}
		}

		/// <summary>
		/// (Server only) A set of owners that have authority on this entity.
		/// </summary>
		/// <value></value>
		public IReadonlyObservableSet<Connection> Owners {
			get {
				if (_group == null)
					throw new InvalidOperationException("Group can only be used on active server entities.");
				return _owners;
			}
			set {
				if (_owners == null)
					throw new InvalidOperationException("Owners can only be used on active server entities.");
				_owners.Target = value;
			}
		}

		/// <summary>
		/// (Server only) A set of owners for this entity that is used by default.
		/// </summary>
		/// <value></value>
		public SubSet<Connection> OwnerSet {
			get {
				if (_owners == null)
					throw new InvalidOperationException("OwnerSet can only be used on active server entities.");
				return _ownerSet;
			}
		}

		/// <summary>
		/// (Server only) Set a connection as the single owner for this entity.
		/// </summary>
		/// <param name="conn"></param>
		public void SetOwner(Connection conn) {
			if (_owners == null)
				throw new InvalidOperationException("OwnerSet can only be used on active server entities.");
			_owners.Target = _ownerSet;
			if (conn == null) {
				_ownerSet.Clear();
			} else {
				_ownerSet.RemoveWhere(c => c != conn);
				_ownerSet.Add(conn);
			}
		}

		/// <summary>
		/// (Client only) True, if the client connection is part of the server entity owner set.
		/// </summary>
		/// <value></value>
		public bool IsMine {
			get { return _isMine; }
		}

		/// <summary>
		/// True, if this entity has been activated. Do NOT use network api otherwise.
		/// </summary>
		/// <value></value>
		public bool Active { get { return _active; } }



#if UNITY_EDITOR
		// Assign unique static entity-ids for each entity per scene:
		private void OnValidate() {
			if (!Application.isPlaying) {
				var ids = new HashSet<int>();
				var isDuplicate = false;
				foreach (var entity in FindObjectsOfType<Entity>()) {
					ids.Add(entity._id.value);
					if (!isDuplicate && entity != this && entity._id == _id)
						isDuplicate = true;
				}

				if (isDuplicate || _id == EntityId.None) {
					_id = EntityId.None;
					for (var val = -1; _id == EntityId.None; val--)
						if (!ids.Contains(val))
							_id = new EntityId(val);
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
		}
#endif

		private void Awake() {
			_untilDeactivate = new Disposable();
		}

		private void OnDestroy() {
			((IEntityInternal)this).Deactivate();
		}

		EntityId IEntityInternal.Id { get { return _id; } set { _id = value; } }

		void IEntityInternal.Activate() {
			if (!_active) {
				if (_id == EntityId.None)
					throw new InvalidOperationException(string.Format("Missing entity id of: {0}", gameObject));
				_map.Add(_id, this);

				var router = EntityRouter.Main;
				if (router.IsServer) {
					if (!_id.IsStatic && _resourcePath == null)
						throw new InvalidOperationException(string.Format("Missing entity resource path for dynamic entity: {0}", gameObject));

					_group = new SetProxy<Connection>();
					_group.Added(_untilDeactivate, conn => {
						if (!_id.IsStatic) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.CREATE);
								payload.Write(_id);
								payload.Write(_resourcePath);
								payload.Write(transform.position);
								payload.Write(transform.rotation);
							});
						}
						ForeachComponent(c => c.Connected(conn));

						if (_owners.Contains(conn)) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.SET_OWNERSHIP);
								payload.Write(_id);
								payload.Write(true);
							});
							ForeachComponent(c => c.OwnerConnected(conn));
						}
					});
					_group.Removed(_untilDeactivate, conn => {
						if (!_id.IsStatic) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.DESTROY);
								payload.Write(_id);
							});
						}
						ForeachComponent(c => c.Disconnected(conn));

						if (_owners.Contains(conn)) {
							ForeachComponent(c => c.OwnerDisconnected(conn));
						}
					});

					_ownerSet = new SubSet<Connection>(router.Connections);
					_owners = new SetProxy<Connection>(_ownerSet);
					_owners.Added(_untilDeactivate, conn => {
						if (_group.Contains(conn)) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.SET_OWNERSHIP);
								payload.Write(_id);
								payload.Write(true);
							});
							ForeachComponent(c => c.OwnerConnected(conn));
						}
						ForeachComponent(c => c.OwnerAdded(conn));
					});
					_ownerSet.Removed(_untilDeactivate, conn => {
						if (_group.Contains(conn)) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.SET_OWNERSHIP);
								payload.Write(_id);
								payload.Write(false);
							});
							ForeachComponent(c => c.OwnerDisconnected(conn));
						}
						ForeachComponent(c => c.OwnerRemoved(conn));
					});
				}

				_active = true;

				foreach(var component in GetComponents<IEntityComponentInternal>()) {
					checked {
						var componentId = _nextComponentId++;
						component.Activate(componentId);
						_components.Add(componentId, component);
					}
				}

				if (router.IsServer) {
					if (_id.IsStatic) {
						_group.Target = router.Scene;
					}
				}
			}
		}

		void IEntityInternal.Deactivate() {
			if (_active ? !(_active = false) : false) {
				_untilDeactivate.Dispose();
				_map.Remove(_id);
				if (_group != null) {
					_group.Target = null;
					_group = null;
				}
				if (_owners != null) {
					_owners = null;
				}
				if (_ownerSet != null) {
					_ownerSet = null;
				}

				try {
					ForeachComponent(c => c.Deactivate());
				} finally {
					_nextComponentId = 0;
					_components.Clear();
				}
			}
		}

		void IEntityInternal.Receive(Message msg) {
			var componentId = msg.ReadByte();
			IEntityComponentInternal component;
			if (_components.TryGetValue(componentId, out component)) {
				component.Receive(msg);
			} else {
				Debug.LogWarningFormat("Remote component missmatch: {0}", componentId);
			}
		}

		void IEntityInternal.SetOwnership(bool isMine) {
			if (_isMine != isMine) {
				_isMine = isMine;
				ForeachComponent(c => c.OwnershipChanged(isMine));
			}
		}

		void ForeachComponent(Action<IEntityComponentInternal> action) {
			foreach (var component in _components) {
				action(component.Value);
			}
		}



		/// <summary>
		/// Load &amp; cache a valid entity resource.
		/// </summary>
		/// <param name="path">The resource path.</param>
		/// <returns></returns>
		public static GameObject GetResource(string path) {
			if (path == null)
				throw new ArgumentNullException("path");

			GameObject res;
			if (_resources.TryGetValue(path, out res)) {
				return res;
			} else {
				res = Resources.Load<GameObject>(path);
				if (!res)
					throw new MissingReferenceException(string.Format("Missing entity resource: {0}", path));
				if (!res.GetComponent<Entity>())
					throw new MissingComponentException(string.Format("Entity resource must have an Entity component: {0}", path));
				_resources.Add(path, res);
				return res;
			}
		}

		/// <summary>
		/// (Server-only) Creates a dynamic entity.
		/// </summary>
		/// <param name="resourcePath">The resource path.</param>
		/// <returns>The local entity instance.</returns>
		public static Entity Create(string resourcePath) {
			if (!EntityRouter.Require().IsServer)
				throw new InvalidOperationException("Dynamic entities must be created on the server.");

			var res = GetResource(resourcePath);
			var id = EntityIds.Allocate();
			var entity = Instantiate(res).GetComponent<Entity>();
			entity._id = id;
			entity._resourcePath = resourcePath;
			((IEntityInternal)entity).Activate();
			return entity;
		}

		/// <summary>
		/// Find an active entity.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public static Entity Find(EntityId id) {
			Entity entity;
			return _map.TryGetValue(id, out entity) ? entity : null;
		}
	}
}
