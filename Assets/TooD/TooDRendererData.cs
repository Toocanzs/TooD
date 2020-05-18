using UnityEngine;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(fileName = "TooD data", menuName = "TooD/TooD data")]
public class TooDRendererData : ScriptableRendererData
{
    protected override ScriptableRenderer Create()
    {
        return new TooDRenderer(this);
    }
}