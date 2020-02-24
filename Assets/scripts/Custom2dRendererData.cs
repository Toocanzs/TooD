using UnityEngine;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(fileName = "Custom2D data", menuName = "2D/Custom 2d")]
public class Custom2dRendererData : ScriptableRendererData
{
    protected override ScriptableRenderer Create()
    {
        return new Custom2dRenderer(this);
    }
}