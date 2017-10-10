using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MatPropsOverride))]
public class MatPropsOverrideEditor : Editor
{
    static GUILayoutOption narrowButton = GUILayout.Width(20.0f);

    // Cache of known shaders and their properties
    static Dictionary<int, List<PropertyFootprint>> shaderProps = new Dictionary<int, List<PropertyFootprint>>();

    // Used to track what shaders are affected by a property and also to cache information
    // about properties
    class PropertyFootprint
    {
        public string property;
        public ShaderPropertyType type;
        public string description;
        public HashSet<Shader> shaders = new HashSet<Shader>();
        public float rangeMin;
        public float rangeMax;
    }

    // Caches the list of properties
    static List<PropertyFootprint> GetShaderProperties(Shader s)
    {
        if (shaderProps.ContainsKey(s.GetInstanceID()))
            return shaderProps[s.GetInstanceID()];

        var pc = ShaderUtil.GetPropertyCount(s);
        var res = new List<PropertyFootprint>();
        for (var i = 0; i < pc; i++)
        {
            var sp = new PropertyFootprint();
            sp.property = ShaderUtil.GetPropertyName(s, i);
            sp.type = (ShaderPropertyType)ShaderUtil.GetPropertyType(s, i);
            sp.description = ShaderUtil.GetPropertyDescription(s, i);
            if (sp.type == ShaderPropertyType.Range)
            {
                sp.rangeMin = ShaderUtil.GetRangeLimits(s, i, 1);
                sp.rangeMax = ShaderUtil.GetRangeLimits(s, i, 2);
            }
            res.Add(sp);
        }
        return shaderProps[s.GetInstanceID()] = res;
    }

    public override void OnInspectorGUI()
    {
        var myMatProps = target as MatPropsOverride;

        EditorGUILayout.Space();

        // Draw header for affected renders
        GUILayout.BeginHorizontal();
        GUILayout.Label("Affected renderers:", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Populate from children"))
        {
            Undo.RecordObject(myMatProps, "Populate");
            myMatProps.Populate();
        }
        GUILayout.EndHorizontal();

        // Draw list of renderers
        EditorGUI.indentLevel += 1;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Renderers"), true);
        if (EditorGUI.EndChangeCheck())
        {
            // Clear out all previously affected renders
            myMatProps.Clear();
            serializedObject.ApplyModifiedProperties();

            // Remove any null elements that may have appeared from user editing
            for (var i = myMatProps.m_Renderers.Count - 1; i >= 0; i--)
            {
                if (myMatProps.m_Renderers[i] == null)
                    myMatProps.m_Renderers.RemoveAt(i);
            }

            // Apply to new list of renderers
            myMatProps.Apply();
        }
        EditorGUI.indentLevel -= 1;

        // Draw properties
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Properties:", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        m_ShowAll = GUILayout.Toggle(m_ShowAll, "Show all", "Button");
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        if (myMatProps.m_Renderers.Count == 0)
        {
            EditorGUILayout.HelpBox("No renderers affected!. This component makes no sense without renderers to affect.", MessageType.Error);
            return;
        }

        // gather all materials, shaders, properties etc.
        var propertyFootprints = new List<PropertyFootprint>();

        foreach (var render in myMatProps.m_Renderers)
        {
            if (render == null)
                continue;
            foreach (var material in render.sharedMaterials)
            {
                if (!material || !material.shader)
                    continue;
                var shaderProperties = GetShaderProperties(material.shader);
                foreach (var shaderProp in shaderProperties)
                {
                    var fp = propertyFootprints.Find(x => x.property == shaderProp.property);
                    if (fp != null)
                    {
                        // Prop already in a footprint; just add this shader
                        fp.shaders.Add(material.shader);
                        continue;
                    }
                    fp = new PropertyFootprint();
                    fp.property = shaderProp.property;
                    fp.type = shaderProp.type;
                    fp.description = shaderProp.description;
                    fp.shaders.Add(material.shader);
                    fp.rangeMax = shaderProp.rangeMax;
                    fp.rangeMin = shaderProp.rangeMin;
                    propertyFootprints.Add(fp);
                }
            }
        }

        // Now we have a list of all properties. Each with a set of shaders it touches

        // Get a list of unique sets of touched shaders
        List<HashSet<Shader>> sets = new List<HashSet<Shader>>();
        foreach (var fp in propertyFootprints)
        {
            if (sets.Find(x => x.SetEquals(fp.shaders)) == null)
                sets.Add(fp.shaders);
        }

        // Sort to get biggest sets of affected shaders first
        sets.Sort((a, b) => b.Count.CompareTo(a.Count));

        // Draw UI
        foreach (var shaderSet in sets)
        {
            // Draw header for this set of shaders
            GUILayout.BeginVertical(EditorStyles.textArea);
            var res = "";
            foreach (var shader in shaderSet)
            {
                res += ", " + shader.name;
            }
            GUILayout.Label("Shaders: " + res.Substring(2));
            GUILayout.EndVertical();

            // Draw properties
            bool anyShown = false;
            foreach (var p in propertyFootprints)
            {
                // Only draw properties that touches this set
                if (!p.shaders.SetEquals(shaderSet))
                    continue;

                // Decide if we should draw
                var propOverride = myMatProps.propertyOverrides.Find(x => x.name == p.property);
                bool hasOverride = propOverride != null;
                if (!hasOverride && !m_ShowAll)
                    continue;

                anyShown = true;
                GUILayout.BeginHorizontal();
                var buttonPressed = false;
                if (m_ShowAll)
                    buttonPressed = GUILayout.Button(hasOverride ? "-" : "+", narrowButton);
                var desc = new GUIContent(p.description, p.property);
                if (!hasOverride)
                {
                    // Draw an non-overridden property. Offer to become overridden
                    GUILayout.Label(desc);
                    if (buttonPressed)
                    {
                        var spv = new MatPropsOverride.ShaderPropertyValue();
                        spv.type = p.type;
                        spv.name = p.property;
                        Undo.RecordObject(myMatProps, "Override");
                        myMatProps.propertyOverrides.Add(spv);
                    }
                }
                else
                {
                    // Draw an overridden property. Offer change of value
                    Undo.RecordObject(myMatProps, "Override");
                    switch (p.type)
                    {
                        case ShaderPropertyType.Color:
                            propOverride.colValue = EditorGUILayout.ColorField(desc, propOverride.colValue);
                            break;
                        case ShaderPropertyType.Float:
                            propOverride.floatValue = EditorGUILayout.FloatField(desc, propOverride.floatValue);
                            break;
                        case ShaderPropertyType.Range:
                            propOverride.floatValue = EditorGUILayout.Slider(desc, propOverride.floatValue, p.rangeMin, p.rangeMax);
                            break;
                        case ShaderPropertyType.Vector:
                            propOverride.vecValue = EditorGUILayout.Vector4Field(desc, propOverride.vecValue);
                            break;
                        case ShaderPropertyType.TexEnv:
                            propOverride.texValue = (Texture)EditorGUILayout.ObjectField(desc, propOverride.texValue, typeof(Texture), false);
                            break;
                    }
                    if (buttonPressed)
                    {
                        myMatProps.propertyOverrides.Remove(propOverride);
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (!anyShown)
            {
                GUILayout.Label("(none)");
            }
        }

        if (GUI.changed)
        {
            myMatProps.Apply();
        }
    }

    bool m_ShowAll = false;
}
