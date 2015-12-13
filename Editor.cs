using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DllUpdate
{
	/// <summary>
	/// The main DLL Update window
	/// </summary>
	public class DllUpdateWindow : EditorWindow
	{
		/// <summary>
		/// Opens the DLL Update window
		/// </summary>
		/// <returns></returns>
		[MenuItem("Window/DLL Update")]
		public static DllUpdateWindow Open()
		{
			return GetWindowWithRect<DllUpdateWindow>(new Rect(200f, 200f, 400f, 300f), true, "DLL Update");
		}

		// Represents an item in the update list
		class ScriptItem
		{
			public ScriptData oldScript;
			public ScriptData newScript;
			public string text; // The typed text
		}

		// Represents a collection of update items, grouped by DLL path
		// This is just for UI/organisation purposes
		class ScriptGroup
		{
			public readonly string dllPath;
			public readonly List<ScriptItem> scripts = new List<ScriptItem>();
			public bool foldout = true;

			public ScriptGroup(string dllPath, IEnumerable<ScriptItem> scripts)
			{
				this.dllPath = dllPath;
				if(scripts != null)
					this.scripts.AddRange(scripts);
			}
		}

		private const float margin = 5f;
		private const float tintAlpha = 0.5f;
		static readonly Color successColor = new Color(0, 1, 0, tintAlpha);
		static readonly Color failureColor = new Color(1, 0, 0, tintAlpha);
		static readonly Color warningColor = new Color(1, 1, 0, tintAlpha);

		readonly List<ScriptGroup> removedScripts = new List<ScriptGroup>(); 
		readonly List<ScriptGroup> existingScripts = new List<ScriptGroup>();

		// The typed text for 'add existing script'
		private string addScript;
		private Vector2 scrollPosition;
		private ScriptItem objectPickerTarget;

		// Adds a new ScriptData item to a given category
		static void AddToGroup(List<ScriptGroup> list, ScriptData script)
		{
			var dllPath = AssetDatabase.GetAssetPath(script.Script);
			var group = list.FirstOrDefault(g => g.dllPath == dllPath);
			if (group == null)
			{
				group = new ScriptGroup(dllPath, null);
				list.Add(group);
			}
			var scriptItem = group.scripts.FirstOrDefault(item => item.oldScript == script);
			if(scriptItem == null)
				group.scripts.Add(new ScriptItem {oldScript = script});
		}

		/// <summary>
		/// Reloads script data
		/// </summary>
		public void RefreshData()
		{
			removedScripts.Clear();
			var data = DllUpdate.GetInstance().CurrentData;
			removedScripts.AddRange(
				data.Where(d => d.Status == ScriptStatus.Deleted)
				.GroupBy(d => AssetDatabase.GetAssetPath(d.Script))
				.Select(group =>
					new ScriptGroup(group.Key, group.Select(d => new ScriptItem {oldScript = d}))));
		}

		protected virtual void OnEnable()
		{
			RefreshData();
		}

		// Gets the color to tint the given item's text field
		// Red: Invalid; Yellow: Possibly invalid object type; Green: Valid
		static Color GetColor(ScriptItem script)
		{
			if (string.IsNullOrEmpty(script.text))
				return Color.white;
			var ms = FindScript(script.text);
			if(ms == null || ms.Status == ScriptStatus.Deleted)
				return failureColor;
			return script.oldScript.Type == ms.Type ? successColor : warningColor;
		}

		// Displays the given script category (removed, existing)
		void ShowScriptCategory(string categoryTitle, List<ScriptGroup> category)
		{
			EditorGUILayout.LabelField(categoryTitle, EditorStyles.boldLabel);
			if(category.Count == 0)
				EditorGUILayout.LabelField("Nothing to display");
			foreach (var group in category)
			{
				group.foldout = EditorGUILayout.Foldout(group.foldout, group.dllPath);
				if (group.foldout)
				{
					EditorGUI.indentLevel++;
					foreach (var script in group.scripts)
					{
						GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
						GUILayout.BeginHorizontal();

						// Draw the text field
						EditorGUILayout.LabelField(script.oldScript.Name, GUILayout.Width(EditorGUIUtility.labelWidth - 15f));
						GUI.color = GetColor(script);
						script.text = EditorGUILayout.TextField(script.text, GUILayout.ExpandWidth(true));
						GUI.color = Color.white;

						// Draw the drop down for quick selection
						var buttonRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
							GUILayout.Width(25f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
						if (GUI.Button(buttonRect, "\u25bc"))
						{
							var data = DllUpdate.GetInstance().CurrentData;
							var items = data
								.Where(d => d.Status == ScriptStatus.New)
								.Where(d => script.oldScript.Type == ScriptType.Unknown
									|| d.Type == script.oldScript.Type)
								.Select(d => new GUIContent((string.IsNullOrEmpty(d.Namespace)
									? "" : (d.Namespace + ".")) + d.Name));
							EditorUtility.DisplayCustomMenu(buttonRect,
								new []{new GUIContent("Choose...")}.Concat(items).ToArray(), -1, SelectMenuItem, script);
						}

						GUILayout.EndHorizontal();
						GUILayout.Space(2*EditorGUIUtility.standardVerticalSpacing);
						script.newScript = FindScript(script.text);
					}
					EditorGUI.indentLevel--;
				}
			}
			EditorGUILayout.Space();
		}

		// Callback when a quick selection is made
		void SelectMenuItem(object userData, string[] options, int selected)
		{
			if (selected > 0)
			{
				((ScriptItem) userData).text = options[selected];
				// This makes sure the text field changes immediately
				EditorGUIUtility.editingTextField = false;
			} else
			{
				objectPickerTarget = (ScriptItem) userData;
				EditorGUIUtility.ShowObjectPicker<MonoScript>(null, false, null, 1);
			}
		}

		protected virtual void OnGUI()
		{
			switch (Event.current.commandName)
			{
				case "ObjectSelectorUpdated":
					var script = (MonoScript)EditorGUIUtility.GetObjectPickerObject();
					objectPickerTarget.text = script == null ? "" : script.GetClass().FullName;
					EditorGUIUtility.editingTextField = false;
					Repaint();
					break;
				case "ObjectSelectorClosed":
					objectPickerTarget = null;
					break;
			}

			const float buttonWidth = 60f;

			GUILayout.BeginArea(new Rect(margin, margin,
				position.width - 2*margin, position.height - 2*margin));
			scrollPosition = GUILayout.BeginScrollView(scrollPosition);

			// 'Show older' button
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Show older", GUILayout.Width(100f)))
			{
				DllUpdate.GetInstance().ShowOlder();
				RefreshData();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Reload scripts", GUILayout.Width(100f)))
			{
				DllUpdate.GetInstance().UpdateData();
				RefreshData();
			}
			GUILayout.EndHorizontal();

			// Script categories
			ShowScriptCategory("Removed scripts", removedScripts);
			ShowScriptCategory("Existing scripts", existingScripts);

			// Add existing script
			GUILayout.BeginHorizontal();
			addScript = EditorGUILayout.TextField("Add script to rename", addScript);
			var addScriptData = FindScript(addScript);
			EditorGUI.BeginDisabledGroup(addScriptData == null
				|| addScriptData.Status == ScriptStatus.Deleted);
			if (GUILayout.Button("Add", GUILayout.Width(buttonWidth)))
			{
				AddToGroup(existingScripts, addScriptData);
				addScript = "";
			}
			EditorGUI.EndDisabledGroup();
			GUILayout.EndHorizontal();
			GUILayout.EndScrollView();
			
			// Control buttons
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("OK", GUILayout.Width(buttonWidth)))
			{
				Apply();
				Close();
			}
			if (GUILayout.Button("Apply", GUILayout.Width(buttonWidth)))
			{
				Apply();
				DllUpdate.GetInstance().UpdateData();
				RefreshData();
			}
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Cancel", GUILayout.Width(buttonWidth)))
				Cancel();
			GUILayout.EndHorizontal();

			GUILayout.EndArea();
		}

		// Finds a ScriptData object matching the given typed name
		static ScriptData FindScript(string name)
		{
			return DllUpdate.GetInstance().CurrentData.FirstOrDefault(
				d => d.Status != ScriptStatus.Deleted &&
				(d.Script.GetClass().FullName == name ||
				 d.Script.name == name));
		}

		void Cancel()
		{
			DllUpdate.GetInstance().SaveChanges();
			Close();
		}

		// Generates a replacement map from the given update category
		static void GenerateReplaceMap(Dictionary<MonoScript, MonoScript> replaceMap, List<ScriptGroup> list)
		{
			foreach (var script in list.SelectMany(group => group.scripts)
				.Where(script => script.newScript != null))
				replaceMap[script.oldScript.Script] = script.newScript.Script;
		}

		void Apply()
		{
			var replaceMap = new Dictionary<MonoScript, MonoScript>();
			GenerateReplaceMap(replaceMap, removedScripts);
			GenerateReplaceMap(replaceMap, existingScripts);
			ScriptReplace.DoReplace(replaceMap);
			DllUpdate.GetInstance().SaveChanges();
		}
	}
}
