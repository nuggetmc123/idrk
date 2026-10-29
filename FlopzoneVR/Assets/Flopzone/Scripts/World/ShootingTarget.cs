using UnityEngine;

namespace Flopzone
{
    /// <summary>A target board for the range; wobbles and dings when shot.</summary>
    public class ShootingTarget : MonoBehaviour
    {
        public static System.Action AnyHit;
        Transform board;
        float wobble;
        Material face;
        int hits;
        TextMesh counter;

        /// <summary>yaw = which way the shooter looks at it (0 = shooter looks toward +Z).</summary>
        public static ShootingTarget Create(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var root = Prims.Empty("target", parent, pos);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var t = root.AddComponent<ShootingTarget>();
            var wood = MapKit.M("6b4a2e", 0.2f);
            Prims.Box(root.transform, new Vector3(-0.35f, 0.7f, 0), new Vector3(0.08f, 1.4f, 0.08f), wood, false);
            Prims.Box(root.transform, new Vector3(0.35f, 0.7f, 0), new Vector3(0.08f, 1.4f, 0.08f), wood, false);
            t.board = Prims.Empty("board", root.transform, new Vector3(0, 1.5f, 0)).transform;
            var disc = Prims.Cyl(t.board, Vector3.zero, 0.9f, 0.06f, MapKit.M("f2efe6", 0.2f), Vector3.forward, true);
            t.face = new Material(disc.GetComponent<Renderer>().sharedMaterial);
            disc.GetComponent<Renderer>().sharedMaterial = t.face;
            Prims.Cyl(t.board, new Vector3(0, 0, -0.035f), 0.66f, 0.01f, MapKit.M("d63c2f", 0.2f), Vector3.forward);
            Prims.Cyl(t.board, new Vector3(0, 0, -0.04f), 0.42f, 0.01f, MapKit.M("f2efe6", 0.2f), Vector3.forward);
            Prims.Cyl(t.board, new Vector3(0, 0, -0.045f), 0.2f, 0.01f, MapKit.M("d63c2f", 0.2f), Vector3.forward);
            t.counter = UIKit.Text(root.transform, "", new Vector3(0, 2.15f, 0), 0.12f, Color.white);
            return t;
        }

        public void Hit(Vector3 point, Vector3 dir)
        {
            wobble = 1f;
            hits++;
            counter.text = hits + (hits == 1 ? " hit" : " hits");
            AudioManager.Sfx("impact", point, 0.8f, 1.4f);
            Fx.I.Impact(point, -dir);
            AnyHit?.Invoke();
        }

        void Update()
        {
            wobble = Mathf.MoveTowards(wobble, 0, Time.deltaTime * 1.5f);
            board.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 18f) * 25f * wobble, 0, 0);
            if (face) face.color = Color.Lerp(Prims.Hex("f2efe6"), new Color(1f, 0.85f, 0.4f), wobble);
        }
    }
}
