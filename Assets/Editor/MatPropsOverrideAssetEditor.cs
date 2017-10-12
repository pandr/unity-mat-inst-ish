using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MatPropsOverrideAsset))]
public class MatPropsOverrideAssetEditor : Editor
{
    private bool m_ShowAll = false;

    public override void OnInspectorGUI()
    {
        var myMatProps = target as MatPropsOverrideAsset;

        EditorGUILayout.Space();

        var headStyle = new GUIStyle("ShurikenModuleTitle");
        headStyle.fixedHeight = 20.0f;
        headStyle.contentOffset = new Vector2(5, -2);
        headStyle.font = EditorStyles.boldFont;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("shader"));

        serializedObject.ApplyModifiedProperties();

        if (myMatProps.shader == null)
        {
            EditorGUILayout.HelpBox("No shader selected!. This asset type needs a shader to make sense.", MessageType.Error);
            return;
        }

        // Draw properties
        EditorGUILayout.Space();
        var re = GUILayoutUtility.GetRect(16f, 22f, headStyle);
        GUI.Box(re, "Properties:", headStyle);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        m_ShowAll = GUILayout.Toggle(m_ShowAll, "Show all", "Button");
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Draw property override GUI
        var shaders = new List<Shader>();
        shaders.Add(myMatProps.shader);
        var changed = MatPropsOverrideEditor.DrawOverrideGUI(shaders, myMatProps.propertyOverrides, ref m_ShowAll, myMatProps);

        if(GUILayout.Button("Select all affected objects in scene"))
        {
            Selection.activeObject = null;
            var objs = new List<GameObject>();
            foreach (var mpo in GameObject.FindObjectsOfType<MatPropsOverride>())
            {
                if (mpo.propertyOverrideAsset == myMatProps)
                {
                    objs.Add(mpo.gameObject);
                }
            }
            Selection.objects = objs.ToArray();
        }

        if (changed)
        {
            // Refresh all objects in scene that uses our override.
            foreach (var mpo in GameObject.FindObjectsOfType<MatPropsOverride>())
            {
                if (mpo.propertyOverrideAsset == myMatProps)
                {
                    mpo.Apply();
                }
            }
            SceneView.RepaintAll();
        }
    }
}

