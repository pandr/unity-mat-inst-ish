using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MatPropsOverrideAsset", menuName = "Assets/MatPropsOverrideAsset")]
public class MatPropsOverrideAsset : ScriptableObject
{
    public Shader shader;
    // This is where the overrides are serialized
    public List<MatPropsOverride.ShaderPropertyValue> propertyOverrides = new List<MatPropsOverride.ShaderPropertyValue>();

}

