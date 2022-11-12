using NUnit.Framework;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(FlaskLabel))]
public class FlaskLabelEditor : Editor
{
    FlaskLabel info;

    void OnEnable()
    {
        info = target as FlaskLabel;
        toAdd = new List<MonoScript>();
    }

    MonoScript selectedElixir;

    List<MonoScript> toAdd;

    MonoScript toRemove;

    private bool notAnElixir;
    public override void OnInspectorGUI()
    {
        
        serializedObject.Update();

        EditorGUILayout.LabelField("Elixirs", EditorStyles.boldLabel);
        if (info == null)
        {
            EditorGUILayout.LabelField("Info is null");
            return;
        }
        if (info.Elixirs == null)
        {
            EditorGUILayout.LabelField("Elixir is null");
            return;
        }

        toRemove = null;
        for (int i = 0; i < info.Elixirs.Length; i++)
        {
            MonoScript type = info.Elixirs[i];
            if (type == null)
                continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(info.elixirNames[i]);
            if (GUILayout.Button("X"))
            {
                toRemove = type;
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField($"There are ({info.Elixirs.Length}) Elixirs total.");
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Add an Elixir", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        MonoScript newElixir = (MonoScript)EditorGUILayout.ObjectField(selectedElixir, typeof(MonoScript), true);
        if (selectedElixir != newElixir)
        {
            selectedElixir = newElixir;
            if (selectedElixir != null)
            {
                Type elixirType = selectedElixir.GetClass();

                Elixir attribute = (Elixir)elixirType.GetCustomAttribute(typeof(Elixir));
                if (attribute == null)
                {
                    notAnElixir = true;
                }
                else
                {
                    notAnElixir = false;
                }
            } else
            {
                notAnElixir = false;
            }
        }

        if (notAnElixir)
        {
            EditorGUILayout.LabelField("THIS IS NOT AN ELIXIR!");
        } else if (selectedElixir != null && GUILayout.Button("Add"))
        {
            toAdd.Add(selectedElixir);
            selectedElixir = null;
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Add All from Current Scene"))
        {
            toAdd.AddRange(Elixir.GetAllElixirsFromScene());
            selectedElixir = null;
        }

        if (toRemove != null || toAdd.Count != 0)
        {
            List<MonoScript> types = new List<MonoScript>();
            types.AddRange(info.Elixirs);
            if (toRemove != null)
                types.Remove(toRemove);

            if (toAdd.Count != 0)
            {
                foreach (MonoScript type in toAdd)
                {
                    if (!types.Contains(type))
                        types.Add(type);
                }
                toAdd.Clear();
            }

            info.Elixirs = types.ToArray();

            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnValidate()
    {
        notAnElixir = false;
        selectedElixir = null;
    }
}
