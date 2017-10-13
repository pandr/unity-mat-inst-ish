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
public class MatPropsOverride : MonoBehaviour
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
    public MatPropsOverrideAsset propertyOverrideAsset = null;

    // List of renderers we are affecting
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

    // Try to do something reasonable when component is added
    private void Reset()
    {
        Clear();
        m_Renderers.Clear();
        m_Renderers.AddRange(GetComponents<Renderer>());
        if(m_Renderers.Count == 0)
        {
            // Fall back, try LODGroup
            var lg = GetComponent<LODGroup>();
            if(lg!=null)
            {
                foreach (var l in lg.GetLODs())
                    m_Renderers.AddRange(l.renderers);
            }
        }
        Apply();
    }

    public void Clear()
    {
        MaterialPropertyBlock mbp = new MaterialPropertyBlock();
        foreach (var r in m_Renderers)
        {
            if (r == null)
                continue;
            r.GetPropertyBlock(mbp);
            mbp.Clear();
            r.SetPropertyBlock(mbp);
        }
    }

    public void Populate()
    {
        m_Renderers.Clear();
        var childRenderers = GetComponentsInChildren<Renderer>();
        if (childRenderers.Length > 100)
        {
            Debug.LogError("Too many renderers.");
            return;
        }
        m_Renderers.AddRange(childRenderers);
    }

    public void Apply()
    {
        // Apply overrides
        MaterialPropertyBlock mbp = new MaterialPropertyBlock();
        foreach (var r in m_Renderers)
        {
            // Can happen when you are editing the through the MatPropsEditor
            if (r == null)
                continue;

            r.GetPropertyBlock(mbp);
            mbp.Clear();
            var overrides = new List<ShaderPropertyValue>();
            if(propertyOverrideAsset != null)
                overrides.AddRange(propertyOverrideAsset.propertyOverrides);
            overrides.AddRange(propertyOverrides);
            foreach (var spv in overrides)
            {
                switch (spv.type)
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
                        if (spv.texValue != null)
                            mbp.SetTexture(spv.name, spv.texValue);
                        break;
                }
            }
            r.SetPropertyBlock(mbp);
        }
    }

}
