using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TooD2
{
    [CreateAssetMenu(fileName = "TooD2 data", menuName = "TooD2/TooD2 data")]
    public class TooD2RendererData : ScriptableRendererData
    {
        protected override ScriptableRenderer Create()
        {
            return new TooD2Renderer(this);
        }
    }
}