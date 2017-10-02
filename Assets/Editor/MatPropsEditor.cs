using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MatProps))]
public class MatPropsEditor : Editor
{
    private static GUILayoutOption narrowButton = GUILayout.Width(20.0f);
    bool showall = false;
    class PropertyFootprint
    {
        public string property;
        public ShaderUtil.ShaderPropertyType type;
        public string description;
        public HashSet<Shader> shaders = new HashSet<Shader>();
    }
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal(EditorStyles.toolbarButton);
        GUILayout.Label("Properties:", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        showall = GUILayout.Toggle(showall, "Show all");
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        var myMatProps = target as MatProps;

        // gather all materials, shaders, properties etc.
        var mats = new List<Material>();
        var propertyFootprints = new List<PropertyFootprint>();

        foreach (var a in myMatProps.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in a.sharedMaterials)
            {
                if (!m || !m.shader)
                    continue;
                mats.Add(m);
                var shaderprops = MatProps.GetShaderProperties(m.shader);
                foreach(var sp in shaderprops)
                {
                    var fp = propertyFootprints.Find(x => x.property == sp.name);
                    if(fp != null)
                    {
                        // Prop already in a footprint; add this shader
                        fp.shaders.Add(m.shader);
                        continue;
                    }
                    fp = new PropertyFootprint();
                    fp.property = sp.name;
                    fp.type = sp.type;
                    fp.description = sp.description;
                    fp.shaders.Add(m.shader);
                    propertyFootprints.Add(fp);
                }
            }
        }

        // Now we have a list of all properties. Each with a set of shaders it touches

        // Get list of unique sets
        List<HashSet<Shader>> sets = new List<HashSet<Shader>>();
        foreach (var fp in propertyFootprints)
        {
            if(sets.Find(x => x.SetEquals(fp.shaders)) == null)
                sets.Add(fp.shaders);
        }

        sets.Sort((a, b) => b.Count.CompareTo(a.Count));

        foreach(var f in sets)
        {
            GUILayout.BeginVertical(EditorStyles.textArea);
            var res = "";
            foreach(var s in f)
            {
                res += ", " + s.name;
            }
            GUILayout.Label("Shaders: " + res.Substring(2));
            GUILayout.EndVertical();
            EditorGUI.indentLevel += 1;
            foreach(var p in propertyFootprints)
            {
                if(p.shaders.SetEquals(f))
                {
                    var propOverride = myMatProps.propertyOverrides.Find(x => x.name == p.property);
                    bool hasOverride = propOverride != null;
                    if (!hasOverride && !showall)
                        continue;
                    GUILayout.BeginHorizontal();
                    var buttonPressed = GUILayout.Button(hasOverride ? "-" : "+", narrowButton);
                    if(!hasOverride)
                    {
                        GUILayout.Label("   Property: " + p.property, hasOverride ? EditorStyles.boldLabel : EditorStyles.label);
                        if(buttonPressed)
                        {
                            var spv = new MatProps.ShaderPropertyValue();
                            spv.type = p.type;
                            spv.name = p.property;
                            Undo.RecordObject(myMatProps, "Override");
                            myMatProps.propertyOverrides.Add(spv);
                        }
                    }
                    else
                    {
                        Undo.RecordObject(myMatProps, "Override");
                        switch(p.type)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                propOverride.colValue = EditorGUILayout.ColorField(p.description, propOverride.colValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                propOverride.floatValue = EditorGUILayout.FloatField(p.description, propOverride.floatValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.Vector:
                                propOverride.vecValue = EditorGUILayout.Vector4Field(p.description, propOverride.vecValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                propOverride.texValue = (Texture)EditorGUILayout.ObjectField(p.description, propOverride.texValue, typeof(Texture), false);
                                break;
                        }
                        if(buttonPressed)
                        {
                            myMatProps.propertyOverrides.Remove(propOverride);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel -= 1;
        }
        if(GUI.changed)
        {
            myMatProps.Apply();
        }
    }
}
