using UnityEditor;
using UnityEngine;

namespace Game.SimpleSFX
{

    [CustomEditor(typeof(SimpleFXSynth))]
    public class SimpleFXEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            using (var cc = new EditorGUI.ChangeCheckScope())
            {
                base.OnInspectorGUI();
                if (GUILayout.Button("Play"))
                {
                    var s = target as SimpleFXSynth;
                    s.Play();
                }
            }
        }

    }
}