using UnityEngine;

namespace Flopzone
{
    /// <summary>Gently spins and bobs a decoration.</summary>
    public class Spinner : MonoBehaviour
    {
        public float speed = 40f, bob;
        Vector3 start;
        void Start() => start = transform.localPosition;
        void Update()
        {
            transform.Rotate(0, speed * Time.deltaTime, 0, Space.World);
            if (bob > 0) transform.localPosition = start + Vector3.up * Mathf.Sin(Time.time * 1.5f) * bob;
        }
    }
}
