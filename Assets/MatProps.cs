using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class MatProps : MonoBehaviour
{
    public class ShaderProperty
    {
        public string name;
        public ShaderUtil.ShaderPropertyType type;
        public string description;
    }

    [System.Serializable]
    public class ShaderPropertyValue
    {
        public string name;
        public ShaderUtil.ShaderPropertyType type;
        public Color colValue;
        public Vector4 vecValue;
        public float floatValue;
        public Texture texValue;
    }

    // Cache of known shaders and their properties
    public static Dictionary<int, List<ShaderProperty>> shaderProps = new Dictionary<int, List<ShaderProperty>>();

    public List<ShaderPropertyValue> propertyOverrides = new List<ShaderPropertyValue>();

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public static List<ShaderProperty> GetShaderProperties(Shader s)
    {
        if (shaderProps.ContainsKey(s.GetInstanceID()))
            return shaderProps[s.GetInstanceID()];

        var pc = ShaderUtil.GetPropertyCount(s);
        var res = new List<ShaderProperty>();
        for(var i = 0; i < pc; i++)
        {
            ShaderProperty sp = new ShaderProperty();
            sp.name = ShaderUtil.GetPropertyName(s, i);
            sp.type = ShaderUtil.GetPropertyType(s, i);
            sp.description = ShaderUtil.GetPropertyDescription(s, i);
            res.Add(sp);
            //Debug.Log(sp.name + ":" + sp.type);
        }
        return shaderProps[s.GetInstanceID()] = res;
    }

    public void Apply()
    {
        MaterialPropertyBlock mbp = new MaterialPropertyBlock();
        foreach(var r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(mbp);
            mbp.Clear();
            foreach(var spv in propertyOverrides)
            {
                switch(spv.type)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        mbp.SetColor(spv.name, spv.colValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        mbp.SetFloat(spv.name, spv.floatValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        mbp.SetVector(spv.name, spv.vecValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        if(spv.texValue != null)
                            mbp.SetTexture(spv.name, spv.texValue);
                        break;
                }
            }
            r.SetPropertyBlock(mbp);
        }
    }

}
