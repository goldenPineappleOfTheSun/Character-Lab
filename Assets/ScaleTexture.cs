using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class ScaleTexture : MonoBehaviour
{
    public float x = 1;
    public float y = 1;

    Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    public void UpdateScaling()
    {
        rend.sharedMaterial.mainTextureScale = new Vector2(x, y);
    }
}

[CustomEditor(typeof(ScaleTexture))]
public class ScaleTextureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ScaleTexture scaleTextureScript = (ScaleTexture)target;

        /* x */

        EditorGUI.BeginChangeCheck();
        float newX = EditorGUILayout.FloatField("x", scaleTextureScript.x);
        if (EditorGUI.EndChangeCheck())
        {
            scaleTextureScript.x = newX;
            scaleTextureScript.UpdateScaling();
        }

        /* y */

        EditorGUI.BeginChangeCheck();
        float newY = EditorGUILayout.FloatField("y", scaleTextureScript.y);
        if (EditorGUI.EndChangeCheck())
        {
            scaleTextureScript.y = newY;
            scaleTextureScript.UpdateScaling();
        }
    }
}