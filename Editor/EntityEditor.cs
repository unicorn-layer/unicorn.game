
using Unicorn.Game.Internal;
using UnityEditor;

namespace Unicorn.Game {
	[CustomEditor(typeof(Entity))]
	public class EntityEditor : Editor {
		public override void OnInspectorGUI() {
			var entity = (Entity)target;
			if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(entity)) || true) {
				var id = ((IEntityInternal)entity).Id;
				EditorGUILayout.LabelField("Id", id.ToString());
				EditorGUILayout.LabelField("Active", entity.Active.ToString());
			}
		}
	}
}
