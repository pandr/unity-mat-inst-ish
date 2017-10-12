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
    public class PropertyFootprint
    {
        public string property;
        public ShaderPropertyType type;
        public string description;
        public HashSet<Shader> shaders = new HashSet<Shader>();
        public float rangeMin;
        public float rangeMax;
    }

    // Caches the list of properties
    public static List<PropertyFootprint> GetShaderProperties(Shader s)
    {
        if (shaderProps.ContainsKey(s.GetInstanceID()))
            return shaderProps[s.GetInstanceID()];

        var res = new List<PropertyFootprint>();
        var pc = ShaderUtil.GetPropertyCount(s);
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

        var headStyle = new GUIStyle("ShurikenModuleTitle");
        headStyle.fixedHeight = 20.0f;
        headStyle.contentOffset = new Vector2(5, -2);
        headStyle.font = EditorStyles.boldFont;

        var re = GUILayoutUtility.GetRect(20, 22, headStyle);
        GUI.Box(re, "Affected renderers", headStyle);

        // Draw header for affected renders
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Populate from children", "Button"))
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
        re = GUILayoutUtility.GetRect(16f, 22f, headStyle);
        GUI.Box(re, "Properties:", headStyle);

        // Draw MatPropOverrideAsset field
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propertyOverrideAsset"), new GUIContent("Property override: asset"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Property override: manual", EditorStyles.boldLabel);
        m_ShowAll = GUILayout.Toggle(m_ShowAll, "Show all", "Button", GUILayout.Width(70));
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (myMatProps.m_Renderers.Count == 0)
        {
            EditorGUILayout.HelpBox("No renderers affected!. This component makes no sense without renderers to affect.", MessageType.Error);
            return;
        }

        // gather all materials, shaders, properties etc.
        var affectedShaders = new List<Shader>();

        if (myMatProps.propertyOverrideAsset != null)
        {
            affectedShaders.Add(myMatProps.propertyOverrideAsset.shader);
        }

        foreach (var render in myMatProps.m_Renderers)
        {
            if (render == null)
                continue;
            foreach (var material in render.sharedMaterials)
            {
                if (!material || !material.shader)
                    continue;
                affectedShaders.Add(material.shader);
            }
        }

        var changed = DrawOverrideGUI(affectedShaders, myMatProps.propertyOverrides, ref m_ShowAll, myMatProps);

        if (changed)
            myMatProps.Apply();
    }

    public static bool DrawOverrideGUI(
        List<Shader> affectedShaders,
        List<MatPropsOverride.ShaderPropertyValue> propertyOverrides,
        ref bool showAll,
        UnityEngine.Object target)
    {
        var headStyle = new GUIStyle("ShurikenModuleTitle");
        headStyle.fixedHeight = 20.0f;
        headStyle.contentOffset = new Vector2(5, -2);
        headStyle.font = EditorStyles.boldFont;

        var propertyFootprints = new List<PropertyFootprint>();

        foreach (var shader in affectedShaders)
        {
            var shaderProperties = GetShaderProperties(shader);
            foreach (var shaderProp in shaderProperties)
            {
                var fp = propertyFootprints.Find(x => x.property == shaderProp.property);
                if (fp != null)
                {
                    // Prop already in a footprint; just add this shader
                    fp.shaders.Add(shader);
                    continue;
                }
                fp = new PropertyFootprint();
                fp.property = shaderProp.property;
                fp.type = shaderProp.type;
                fp.description = shaderProp.description;
                fp.shaders.Add(shader);
                fp.rangeMax = shaderProp.rangeMax;
                fp.rangeMin = shaderProp.rangeMin;
                propertyFootprints.Add(fp);
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
            var res = "";
            foreach (var shader in shaderSet)
            {
                res += (res.Length > 0 ? ", " : "") + shader.name;
            }
            headStyle.font = EditorStyles.standardFont;
            var re = GUILayoutUtility.GetRect(16f, 22f, headStyle);
            GUI.Box(re, "Affected shaders: " + res, headStyle);

            // Draw properties
            bool anyShown = false;
            foreach (var p in propertyFootprints)
            {
                // Only draw properties that touches this set
                if (!p.shaders.SetEquals(shaderSet))
                    continue;

                // Decide if we should draw
                var propOverride = propertyOverrides.Find(x => x.name == p.property);
                bool hasOverride = propOverride != null;
                if (!hasOverride && !showAll)
                    continue;

                anyShown = true;
                GUILayout.BeginHorizontal();
                var buttonPressed = false;
                if (showAll)
                    buttonPressed = GUILayout.Button(hasOverride ? "-" : "+", GUILayout.Width(20));
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
                        Undo.RecordObject(target, "Override");
                        propertyOverrides.Add(spv);
                    }
                }
                else
                {
                    // Draw an overridden property. Offer change of value
                    Undo.RecordObject(target, "Override");
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
                        propertyOverrides.Remove(propOverride);
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (!anyShown)
            {
                GUILayout.Label("(none)");
            }
        }

        return GUI.changed;
    }

    bool m_ShowAll = false;
}
