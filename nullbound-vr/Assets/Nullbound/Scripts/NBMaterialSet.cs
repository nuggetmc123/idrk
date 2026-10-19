using UnityEngine;

namespace Nullbound
{
    /// Material templates. The editor setup saves real material assets into Resources so the
    /// shaders (and their transparent variants) are guaranteed to be in the Quest build.
    public class NBMaterialSet : ScriptableObject
    {
        public Material lit;
        public Material glow;
        public Font font;
    }
}
