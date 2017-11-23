
using System;
using System.Collections.Generic;
using Unicorn.IO;
using Unicorn.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unicorn.Game.Internal {
	public class EntityRouter : Router {
		private static EntityRouter _main;
		public static EntityRouter Main {
			get { return _main; }
		}

		private SubSet<Connection> _scene;
		public IReadonlyObservableSet<Connection> Scene { get { return _scene; } }

		private string _managerResourcePath;
		private Disposable _untilStopped = new Disposable();

		public static EntityRouter Require() {
			if (_main == null)
				throw new InvalidOperationException("No entity router is initialized.");
			return _main;
		}
		
		public EntityRouter(RouterConfig config, string managerResourcePath) : base(config) {
			_main = this;
			_scene = new SubSet<Connection>(Connections);
			_managerResourcePath = managerResourcePath;
		}

		protected override void Receive(Message msg) {
			var targetEntityId = msg.ReadEntityId();
			if (targetEntityId == EntityId.None) {
				if (IsClient) {
					var controlCode = msg.ReadByte();
					switch (controlCode) {
						case ClientMessage.CREATE:
							Client_CreateEntity(
								msg.ReadEntityId(),
								msg.ReadString(),
								msg.ReadVector3(),
								msg.ReadQuaternion());
							break;

						case ClientMessage.DESTROY:
							Client_DestroyEntity(msg.ReadEntityId());
							break;
							
						default:
							Debug.LogWarningFormat("Unknown client control code: {0}", controlCode);
							break;
					}
				} else {
					var controlCode = msg.ReadByte();
					switch (controlCode) {
						case ServerMessage.SET_CLIENT_SCENE:
							Server_SetClientScene(msg.ReadString(), msg.Sender);
							break;

						default:
							Debug.LogWarningFormat("Unknown server control code: {0}", controlCode);
							break;
					}
				}
			}
		}

		private void Client_CreateEntity(EntityId id, string resourcePath, Vector3 position, Quaternion rotation) {
			var res = Entity.GetResource(resourcePath);
			var entity = UnityObject.Instantiate(res, position, rotation).GetComponent<Entity>();
			((IEntityInternal)entity).Id = id;
			((IEntityInternal)entity).Activate();
		}

		private void Client_DestroyEntity(EntityId id) {
			var entity = Entity.Find(id);
			if (entity)
				UnityObject.Destroy(entity);
		}
		
		private void Server_SetClientScene(string sceneName, Connection client) {
			if (SceneManager.GetActiveScene().name == sceneName) {
				_scene.Add(client);
			} else {
				_scene.Remove(client);
			}
		}
		
		protected override void Started() {
			base.Started();
			SceneManager.sceneLoaded += SceneLoaded;
			ActivateStaticEntities();

			if (IsServer) {
				if (!string.IsNullOrEmpty(_managerResourcePath)) {
					Entity.Create(_managerResourcePath).Group = Connections;
				}
			} else {
				Connections.Added(_untilStopped, conn => {
					conn.Send(payload => {
						payload.Write(EntityId.None);
						payload.Write(ServerMessage.SET_CLIENT_SCENE);
						payload.Write(SceneManager.GetActiveScene().name);
					});
				});
			}
		}

		protected override void Stopped() {
			base.Stopped();
			SceneManager.sceneLoaded -= SceneLoaded;
			DeactivateStaticEntities();
			DestroyDynamicEntities();
			EntityIds.Reset();

			_untilStopped.Dispose();
		}

		private void SceneLoaded(Scene scene, LoadSceneMode mode) {
			if (IsClient) {
				Connections.Send(payload => {
					payload.Write(EntityId.None);
					payload.Write(ServerMessage.SET_CLIENT_SCENE);
					payload.Write(scene.name);
				});
			}
			
			if (IsServer) {
				_scene.Clear();
			}

			ActivateStaticEntities();
		}

		private IEnumerable<Entity> GetLoadedEntities() {
			return UnityObject.FindObjectsOfType<Entity>();
		}

		private void ActivateStaticEntities() {
			foreach (var entity in GetLoadedEntities()) {
				if (((IEntityInternal)entity).Id.IsStatic) {
					((IEntityInternal)entity).Activate();
				}
			}
		}

		private void DeactivateStaticEntities() {
			foreach (var entity in GetLoadedEntities()) {
				if (((IEntityInternal)entity).Id.IsStatic) {
					((IEntityInternal)entity).Deactivate();
				}
			}
		}

		private void DestroyDynamicEntities() {
			foreach(var entity in GetLoadedEntities()) {
				if (!((IEntityInternal)entity).Id.IsStatic) {
					UnityObject.Destroy(entity.gameObject);
				}
			}
		}
	}
}
