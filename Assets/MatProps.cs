using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShaderPropertyType
{
    Color = 0,
    Vector = 1,
    Float = 2,
    Range = 3,
    TexEnv = 4
}

[ExecuteInEditMode]
public class MatProps : MonoBehaviour
{
    [System.Serializable]
    public class ShaderPropertyValue
    {
        public string name;
        public ShaderPropertyType type;
        public Color colValue;
        public Vector4 vecValue;
        public float floatValue;
        public Texture texValue;
    }

    // This is where the overrides are serialized
    public List<ShaderPropertyValue> propertyOverrides = new List<ShaderPropertyValue>();

    // List of renderers we are affecting
    [System.NonSerialized]
    public List<Renderer> m_Renderers = new List<Renderer>();

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void Clear()
    {
        MaterialPropertyBlock mbp = new MaterialPropertyBlock();
        foreach(var r in m_Renderers)
        {
            r.GetPropertyBlock(mbp);
            mbp.Clear();
            r.SetPropertyBlock(mbp);
        }
    }

    public void Apply()
    {
        // Refresh list of renderers to apply to
        m_Renderers.Clear();

        // First look for locally attached render components
        foreach(var r in GetComponents<Renderer>())
        {
            m_Renderers.Add(r);
        }

        // If none found, look for a local LODGroup
        if(m_Renderers.Count == 0)
        {
            foreach(var lg in GetComponents<LODGroup>())
            {
                foreach(var l in lg.GetLODs())
                {
                    m_Renderers.AddRange(l.renderers);
                }
            }
        }

        // Apply overrides
        MaterialPropertyBlock mbp = new MaterialPropertyBlock();
        foreach(var r in m_Renderers)
        {
            r.GetPropertyBlock(mbp);
            mbp.Clear();
            foreach(var spv in propertyOverrides)
            {
                switch(spv.type)
                {
                    case ShaderPropertyType.Color:
                        mbp.SetColor(spv.name, spv.colValue);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        mbp.SetFloat(spv.name, spv.floatValue);
                        break;
                    case ShaderPropertyType.Vector:
                        mbp.SetVector(spv.name, spv.vecValue);
                        break;
                    case ShaderPropertyType.TexEnv:
                        if(spv.texValue != null)
                            mbp.SetTexture(spv.name, spv.texValue);
                        break;
                }
            }
            r.SetPropertyBlock(mbp);
        }
    }

}
