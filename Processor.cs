using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace DllUpdate
{
	/// <summary>
	/// Catches DLL import events
	/// </summary>
	public class DllUpdateProcessor : AssetPostprocessor
	{
		public static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			var obj = DllUpdate.GetInstance();
			obj.UpdateData();
			if(obj.CheckData())
				DllUpdateWindow.Open().RefreshData();
		}
    }

	/// <summary>
	/// Handles script processing and stores session-persistent data
	/// </summary>
	[InitializeOnLoad]
	public class DllUpdate : ScriptableObject
	{
		[Serializable]
		protected struct ScriptTypePair
		{
			public MonoScript script;
			public ScriptType type;
		}

		// Cache some lists here to persist throughout the session:
		//     No way to detect new scripts, but we can save old ones
		[SerializeField] protected List<MonoScript> oldScripts = new List<MonoScript>();
		//     MonoScript instances are always saved, so we need to make sure they don't keep coming back
		[SerializeField] protected List<MonoScript> ignoreScripts = new List<MonoScript>();
		//     This saves the script types so we know which assignments are valid
		[SerializeField] protected List<ScriptTypePair> scriptTypes = new List<ScriptTypePair>();

		private readonly List<ScriptData> currentData
			= new List<ScriptData>();

		private Dictionary<MonoScript, ScriptType> typeCache;

		/// <summary>
		/// The currently loaded scripts
		/// </summary>
		public IList<ScriptData> CurrentData => currentData.AsReadOnly(); 

		private static DllUpdate instance;

		/// <summary>
		/// Gets a DllUpdate instance
		/// </summary>
		/// <returns></returns>
		public static DllUpdate GetInstance()
		{
			instance = instance ??
				Resources.FindObjectsOfTypeAll<DllUpdate>().FirstOrDefault() ??
				CreateInstance<DllUpdate>();
			return instance;
		}

		static DllUpdate()
		{
			// Make sure that an instance always exists, so that we can detect changes in scripts
			EditorApplication.update += () => GetInstance();
		}

		public DllUpdate()
		{
			// Unity doesn't like doing things in constructors, so delay the first update
			EditorApplication.update += DelayedUpdate;
		}

		void DelayedUpdate()
		{
			UpdateData();
			BuildTypeCache();
			SaveTypeCache(); // Generate initial type data
			EditorUtility.SetDirty(this);
			EditorApplication.update -= DelayedUpdate;
		}

		/// <summary>
		/// Clears the ignore list, causing old deleted scripts to be visible
		/// </summary>
		public void ShowOlder()
		{
			ignoreScripts.Clear();
			UpdateData();
		}

		void BuildTypeCache()
		{
			typeCache = scriptTypes
				.Where(pair => pair.script != null)
				.ToDictionary(pair => pair.script, pair => pair.type);
		}

		ScriptType GetScriptType(MonoScript script)
		{
			ScriptType type;
			typeCache.TryGetValue(script, out type);
			return type;
		}

		void SaveTypeCache()
		{
			foreach (var d in currentData.Where(d => d.Status != ScriptStatus.Deleted))
				typeCache[d.Script] = d.Type;
			scriptTypes.Clear();
			scriptTypes.AddRange(typeCache.Select(pair => new ScriptTypePair { script = pair.Key, type = pair.Value }));
		}

		/// <summary>
		/// Finalises script changes,
		/// </summary>
		public void SaveChanges()
		{
			BuildTypeCache();
			SaveTypeCache();
			foreach (var d in currentData)
			{
				switch (d.Status)
				{
					case ScriptStatus.New:
						oldScripts.Add(d.Script);
						break;
					case ScriptStatus.Deleted:
						ignoreScripts.Add(d.Script);
						break;
					case ScriptStatus.Old:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			currentData.Clear();
			EditorUtility.SetDirty(this);
		}

		// Checks that an object is a user asset, not an internal one
		private static bool CheckAsset(UnityEngine.Object obj)
		{
			return AssetDatabase.GetAssetPath(obj).StartsWith("Assets", StringComparison.OrdinalIgnoreCase);
		}

		public void UpdateData()
		{
			BuildTypeCache();
			currentData.Clear();
			// Load all scripts
			currentData.AddRange(Resources.FindObjectsOfTypeAll<MonoScript>()
				// Ignore system scripts
				.Where(CheckAsset)
				// Ignore DllUpdate scripts
				.Where(script => !Equals(script.GetClass()?.Assembly, typeof (DllUpdate).Assembly))
				// Ignore those that were deleted before (unless they're not deleted now)
				.Where(script => !ignoreScripts.Contains(script) || script.GetClass() != null).Select(script => new ScriptData(script: script, type: script.GetClass() != null ? (script.GetClass().IsSubclassOf(typeof (ScriptableObject)) ? ScriptType.ScriptableObject : ScriptType.MonoBehaviour) : (GetScriptType(script)), old: oldScripts.Contains(script))));
		}

		/// <summary>
		/// Returns <c>true</c> if any scripts were deleted
		/// </summary>
		/// <returns></returns>
		public bool CheckData()
		{
			return currentData.Any(d => d.Status == ScriptStatus.Deleted);
		}
	}
}
