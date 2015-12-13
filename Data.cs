using System;
using UnityEditor;
using UnityEngine;

namespace DllUpdate
{
	/// <summary>
	/// Indicates the class of objects the script can be applied to
	/// </summary>
	public enum ScriptType
	{
		Unknown,
		MonoBehaviour,
		ScriptableObject,
	}

	/// <summary>
	/// Indicates the script's temporal status
	/// </summary>
	public enum ScriptStatus
	{
		New,
		Old,
		Deleted
	}

	/// <summary>
	/// Stores data relating to a given (ex)-script
	/// </summary>
	[Serializable]
	public class ScriptData
	{
		[SerializeField]
		protected MonoScript script;
		[SerializeField]
		protected ScriptType type;
		[SerializeField]
		protected string name;
		[SerializeField]
		protected string nameSpace;
		[SerializeField]
		protected bool old;

		public MonoScript Script => script;
		public ScriptType Type => type;
		public string Name => name;
		public string Namespace => nameSpace;

		public ScriptStatus Status
			=> script?.GetClass() == null ? ScriptStatus.Deleted :
			(old ? ScriptStatus.Old : ScriptStatus.New);

		public ScriptData()
		{
		}

		public ScriptData(MonoScript script, ScriptType type, bool old)
		{
			this.script = script;
			name = script.name;
			nameSpace = (new SerializedObject(script)).FindProperty("m_Namespace").stringValue;
			this.old = old;
			this.type = type;
		}
	}
}
