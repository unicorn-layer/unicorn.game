
using Unicorn.Game.Internal;
using UnityEditor;

namespace Unicorn.Game {
	[CustomEditor(typeof(Entity))]
	public class EntityEditor : Editor {
		private static bool _showConnections;
		private static bool _showOwners;

		public override void OnInspectorGUI() {
			var entity = (Entity)target;
			var router = EntityRouter.Main;

			if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(entity.gameObject))) {
				var id = ((IEntityInternal)entity).Id;
				EditorGUILayout.LabelField("Id", id.ToString());
				EditorGUILayout.LabelField("Active", entity.Active.ToString());

				if (entity.Active && router != null) {
					if (router.IsServer) {
						_showConnections = EditorGUILayout.Foldout(_showConnections, string.Format("Connections ({0})", entity.Group.Count));
						if (_showConnections) {
							ConnectionEditorGUI.Draw(entity.Group);
						}
						_showOwners = EditorGUILayout.Foldout(_showOwners, string.Format("Owners ({0})", entity.Owners.Count));
						if (_showOwners) {
							ConnectionEditorGUI.Draw(entity.Owners);
						}
					} else if (router.IsClient) {
						EditorGUILayout.LabelField("Is Mine", entity.IsMine.ToString());
					}
				}
			}
		}
	}
}
