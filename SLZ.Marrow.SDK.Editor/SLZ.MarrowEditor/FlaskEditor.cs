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
            if (GUILayout.Button("Pour Flasks in Pallet"))
            {
                Flask flask = (Flask)target;

                ElixirMixer.ExportFlasks(flask.Pallet);

                string palletPath = Path.GetFullPath(ModBuilder.BuildPath);
                string flaskPath = Path.Combine(palletPath, "flasks");
                EditorUtility.RevealInFinder(flaskPath);
            }
            if (GUILayout.Button("Stir Flask"))
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
                /*if (stirred)
                   */
            }
            //EditorGUILayout.PropertyField(flaskInfoProperty);
        }

        private static void OnBuildComplete(bool hasErrors)
        {
            if (hasErrors)
            {
                /*if (EditorUtility.DisplayDialog("Error", $"Errors detected in the Flask! Check the Console for errors.", "View generated scripts", "Done"))
                {

                }*/
            }
            else EditorUtility.DisplayDialog("Yay", "Stirred successfully with no anomalies!", "Drink the grog");

            if (EditorUtility.DisplayDialog("Okay", "[DEBUG] Open the folder .dll please", "OK"))
            {
                EditorUtility.RevealInFinder(Path.Combine(Application.temporaryCachePath, "StirTest.dll"));
            }
        }
    }

    [CustomPreview(typeof(Flask))]
    public class FlaskPreview : CratePreview { }
#endif
}