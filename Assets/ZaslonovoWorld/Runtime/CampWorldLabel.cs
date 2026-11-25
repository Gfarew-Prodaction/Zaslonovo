using UnityEngine;

namespace Zaslonovo
{
    [ExecuteAlways,RequireComponent(typeof(TextMesh),typeof(MeshRenderer))]
    public sealed class CampWorldLabel : MonoBehaviour
    {
        TextMesh label;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        void OnEnable(){label=GetComponent<TextMesh>();meshRenderer=GetComponent<MeshRenderer>();Font.textureRebuilt+=FontRebuilt;ApplyAtlas();}
        void OnDisable(){Font.textureRebuilt-=FontRebuilt;}
        void FontRebuilt(Font changed){if(label!=null&&label.font==changed)ApplyAtlas();}
        void LateUpdate(){ApplyAtlas();}
        void ApplyAtlas()
        {
            if(label==null||label.font==null||meshRenderer==null)return;
            properties??=new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(properties);properties.SetTexture("_MainTex",label.font.material.mainTexture);meshRenderer.SetPropertyBlock(properties);
        }
    }
}
