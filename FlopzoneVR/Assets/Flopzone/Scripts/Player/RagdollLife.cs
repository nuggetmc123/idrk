using UnityEngine;

namespace Flopzone
{
    /// <summary>Lets a ragdoll lie there for a bit, then sinks it into the floor.</summary>
    public class RagdollLife : MonoBehaviour
    {
        float born;
        bool sinking;
        public static int Count;

        void Start()
        {
            born = Time.time;
            Count++;
        }

        void OnDestroy() => Count--;

        void Update()
        {
            float age = Time.time - born;
            if (!sinking && (age > 9f || (Count > 8 && age > 3f)))
            {
                sinking = true;
                foreach (var rb in GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            if (sinking)
            {
                transform.position += Vector3.down * Time.deltaTime * 0.5f;
                if (age > 12f) Destroy(gameObject);
            }
        }
    }
}
