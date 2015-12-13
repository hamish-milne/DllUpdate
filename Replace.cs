using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DllUpdate
{
	/// <summary>
	/// Script replacement utility
	/// </summary>
	public static class ScriptReplace
	{
		private struct ObjectId
		{
			public int id;
			public ScriptType type;
			public SerializedProperty script;
		}

		// Traverses the asset folder, finding all ScriptableObject assets
		private static IEnumerable<ObjectId> GetScriptableObjects()
		{
			var hp = new HierarchyProperty(HierarchyType.Assets);
			var expanded = new List<int>();
			var eArr = expanded.ToArray();
			while (hp.Next(eArr))
			{
				if (hp.hasChildren)
				{
					expanded.Add(hp.instanceID);
					eArr = expanded.ToArray();
				}
				else if (hp.pptrValue == null || hp.pptrValue is ScriptableObject)
					yield return new ObjectId { id = hp.instanceID, type = ScriptType.ScriptableObject };
			}
		}

		// Uses the ActiveEditorTracker to find the m_Script properties of the given object
		// There is *no other way* to do this that won't return nulls for 'script missing' objects
		private static IEnumerable<ObjectId> GetScriptProperties(ObjectId obj)
		{
			Selection.instanceIDs = new[] { obj.id };
			ActiveEditorTracker.sharedTracker.ForceRebuild();
			return ActiveEditorTracker.sharedTracker.activeEditors
				.Select(editor => editor.serializedObject.FindProperty("m_Script"))
				.Where(prop => prop != null)
				.Select(prop => new ObjectId { id = obj.id, type = obj.type, script = prop });
		}

		/// <summary>
		/// Replaces MonoScript references throughout a project
		/// </summary>
		/// <param name="replaceMap">A map where the keys are the original scripts, the values are the replacement</param>
		public static void DoReplace(Dictionary<MonoScript, MonoScript> replaceMap)
		{
			if(replaceMap == null)
				throw new ArgumentNullException(nameof(replaceMap));
			var successCount = 0;
			var failureCount = 0;

			var oldSelection = Selection.instanceIDs;
			var oldActive = Selection.activeInstanceID;
			// Make sure all prefabs are loaded
			foreach (var path in AssetDatabase.FindAssets("t:prefab"))
				AssetDatabase.LoadAssetAtPath(path, typeof (GameObject));
			var all = Resources.FindObjectsOfTypeAll<GameObject>()
				.Select(go => new ObjectId { id = go.GetInstanceID(), type = ScriptType.MonoBehaviour })
				.Concat(GetScriptableObjects())
				.SelectMany(GetScriptProperties);
			foreach (var obj in all)
			{
				var prop = obj.script;
				MonoScript replace;
				// ReSharper disable once RedundantCast.0
				if ((object)prop.objectReferenceValue == null)
					continue;
				replaceMap.TryGetValue((MonoScript)prop.objectReferenceValue, out replace);
				if (replace?.GetClass() == null) continue;
				if (replace.GetClass().IsSubclassOf(typeof(ScriptableObject))
					&& obj.type == ScriptType.MonoBehaviour)
				{
					failureCount++;
					continue;
				}
				prop.objectReferenceValue = replace;
				prop.serializedObject.ApplyModifiedProperties();
				successCount++;
			}
			Resources.UnloadUnusedAssets();
			Selection.instanceIDs = oldSelection;
			Selection.activeInstanceID = oldActive;
			Debug.Log($"Script replacement complete: {successCount} replaced, {failureCount} invalid objects");
		}
	}
}
