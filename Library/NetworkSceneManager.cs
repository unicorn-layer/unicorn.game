
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
				SceneManager.LoadSceneAsync(offlineScene);
			}
		}

		protected override void OnEntityClientConnected(Connection conn) {
			base.OnEntityClientConnected(conn);
			var currentScene = SceneManager.GetActiveScene().name;

			if (synchronizeOfflineScene || (currentScene != offlineScene)) {
				Send(conn, LoadScene(currentScene));
			}
		}



		private void ServerSceneLoaded(Scene scene, LoadSceneMode mode) {
			Send(LoadScene(scene.name));
		}



		private enum Msg : byte { LoadScene }

		[Client((byte)Msg.LoadScene)]
		private void LoadScene(Message msg) {
			SceneManager.LoadSceneAsync(msg.ReadString());
		}

		private MessageWriter LoadScene(string name) {
			return payload => {
				Endpoint(payload, (byte)Msg.LoadScene);
				payload.Write(name);
			};
		}
	}
}
