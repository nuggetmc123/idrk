using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Marks the car showroom that's saved into the scene so you can see the models without pressing Play.
    /// The game builds its own world at runtime, so this hides itself as soon as Play starts (and it's tagged
    /// EditorOnly, so it never goes into a build).
    /// </summary>
    public class EditorPreview : MonoBehaviour
    {
        void Awake()
        {
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
