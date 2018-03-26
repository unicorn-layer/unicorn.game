
using System;
using Unicorn.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unicorn.Game {
	public sealed class NetworkSceneManager : EntityComponent<NetworkSceneManager> {
		[Tooltip("The scene to be loaded when the scene manager is deactivated.")]
		public string offlineScene = "";
		[Tooltip("True, to set the current scene as offline scene when the scene manager is instantiated.")]
		public bool autoAssignOfflineScene = true;
		[Tooltip("True, to load the offline scene on clients if loaded on the server. Even if not, the offline scene is loaded on clients when loaded on the server while the scene manager is active.")]
		public bool synchronizeOfflineScene = false;

		private Action<string> _loadScene = null;

		/// <summary>
		/// Set a custom method for loading a scene asynchronously.
		/// </summary>
		/// <param name="loadScene">The custom method or <c>null</c> to use the default.</param>
		public void SetLoadScene(Action<string> loadScene) {
			_loadScene = loadScene;
		}

		private void Awake() {
			DontDestroyOnLoad(gameObject);
			if (autoAssignOfflineScene) {
				offlineScene = SceneManager.GetActiveScene().name;
			}
		}

		protected override void OnEntityActivate() {
			base.OnEntityActivate();
			if (EntityNetwork.IsServer) {
				SceneManager.sceneLoaded += ServerSceneLoaded;
			}
		}

		protected override void OnEntityDeactivate() {
			base.OnEntityDeactivate();
			if (EntityNetwork.IsServer) {
				SceneManager.sceneLoaded -= ServerSceneLoaded;
			}
			if (!string.IsNullOrEmpty(offlineScene)) {
				LoadLocalScene(offlineScene);
			}
		}

		protected override void OnEntityClientConnected(Connection conn) {
			base.OnEntityClientConnected(conn);
			var currentScene = SceneManager.GetActiveScene().name;

			if (synchronizeOfflineScene || (currentScene != offlineScene)) {
				Send(conn, LoadScene(currentScene));
			}
		}



		private void LoadLocalScene(string name) {
			if (_loadScene == null) {
				SceneManager.LoadSceneAsync(name);
			} else {
				_loadScene(name);
			}
		}

		private void ServerSceneLoaded(Scene scene, LoadSceneMode mode) {
			Send(LoadScene(scene.name));
		}



		private enum Msg : byte { LoadScene }

		[Client((byte)Msg.LoadScene)]
		private void LoadScene(Message msg) {
			LoadLocalScene(msg.ReadString());
		}

		private MessageWriter LoadScene(string name) {
			return payload => {
				Endpoint(payload, (byte)Msg.LoadScene);
				payload.Write(name);
			};
		}
	}
}
