
using System;
using System.Collections.Generic;
using Unicorn.Game.Internal;
using Unicorn.IO;
using Unicorn.Util;
using UnityEngine;

namespace Unicorn.Game {
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public sealed class Entity : MonoBehaviour, IEntityInternal {
		[SerializeField]
		private EntityId _id = EntityId.None;

		// (Server & dynamic-entity only)
		private string _resourcePath;

		// True, if the entity has been activated.
		private bool _active;
		// Disposed when the entity is deactivated.
		private Disposable _untilDeactivate;

		// (Server only) Set of clients for which will be instantiated.
		private SetProxy<Connection> _group;
		public IReadonlyObservableSet<Connection> Group {
			get { return _group; }
			set {
				if (_group == null)
					throw new InvalidOperationException("Group must be set on the server.");
				if (_id.IsStatic)
					throw new InvalidOperationException("Group cannot be set for static entities.");
				_group.Target = value;
			}
		}

		// Map of entity-ids to active entities.
		private static SortedDictionary<EntityId, Entity> _map = new SortedDictionary<EntityId, Entity>();

		// Map of resource paths to valid entity resources:
		private static SortedDictionary<string, GameObject> _resources = new SortedDictionary<string, GameObject>();
		
		// Map of component-ids to.
		private SortedDictionary<byte, IEntityComponentInternal> _components = new SortedDictionary<byte, IEntityComponentInternal>();
		// Id which will be assigned to the next component.
		private byte _nextComponentId;



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

					// TODO:
					// Instantiate on client when client is added to the group.
					// Destroy on client when client is removed from the group.
					// Set remote ownership when added to the group & client is an owner.
					// Set remote ownership when client is added as owner & part of the group.
					// Remove remote ownership when client is removed as owner & part of the group.

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

						foreach (var component in _components)
							component.Value.Connected(conn);
					});
					_group.Removed(_untilDeactivate, conn => {
						if (!_id.IsStatic) {
							conn.Send(payload => {
								payload.Write(EntityId.None);
								payload.Write(ClientMessage.DESTROY);
								payload.Write(_id);
							});
						}

						foreach (var component in _components)
							component.Value.Disconnected(conn);
					});
					
					// TODO: Initialize owner set.
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
				_map.Remove(_id);
				_group.Target = null;

				try {
					foreach (var component in _components)
						component.Value.Deactivate();
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
