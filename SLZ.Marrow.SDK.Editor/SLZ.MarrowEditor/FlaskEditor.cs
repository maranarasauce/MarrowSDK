using UnityEngine;
using UnityEditor;
using SLZ.Marrow.Warehouse;
using Maranara.Marrow;
using UnityEngine.Events;
using System.IO;

namespace SLZ.MarrowEditor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(Flask))]
    [CanEditMultipleObjects]
    public class FlaskEditor : CrateEditor
    {
        SerializedProperty flaskInfoProperty;

        public override void OnEnable()
        {
            base.OnEnable();
            flaskInfoProperty = serializedObject.FindProperty("_mainAsset");
        }
        public override void OnInspectorGUIBody()
        {
            base.OnInspectorGUIBody();

            GUILayout.Space(20);
            if (GUILayout.Button("Stir Flasks"))
            {
                Flask flask = (Flask)target;

                string title = "StirTest";
                string buildPath = Application.temporaryCachePath;

                UnityEvent<bool> BuildEvent = new UnityEvent<bool>();
                BuildEvent.AddListener((hasErrors) =>
                {
                    ElixirMixer.TreatExportedElixir(Path.Combine(buildPath, title + ".dll"));
                });
                BuildEvent.AddListener(OnBuildComplete);

                ElixirMixer.ExportElixirs("StirTest", buildPath, flask, BuildEvent);
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Stir Flasks into Pallet"))
            {
                Flask flask = (Flask)target;

                ElixirMixer.ExportFlasks(flask.Pallet);

                string palletPath = Path.GetFullPath(ModBuilder.BuildPath);
                string flaskPath = Path.Combine(palletPath, "flasks");
                EditorUtility.RevealInFinder(flaskPath);
            }
            GUILayout.Space(20);
        }

        private static void OnBuildComplete(bool hasErrors)
        {
            if (hasErrors)
            {
                
            }
            else EditorUtility.DisplayDialog("Yay", "Stirred successfully with no anomalies!", "Drink the grog");

            if (EditorUtility.DisplayDialog("Flask stirring complete.", "Would you like to open the compiled folder?", "Yes"))
            {
                EditorUtility.RevealInFinder(Path.Combine(Application.temporaryCachePath, "StirTest.dll"));
            }
        }
    }

    [CustomPreview(typeof(Flask))]
    public class FlaskPreview : CratePreview { }
#endif
}