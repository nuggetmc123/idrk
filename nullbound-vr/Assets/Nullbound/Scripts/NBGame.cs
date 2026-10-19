using UnityEngine;

namespace Nullbound
{
    /// Starts everything. The scene only holds the NetworkManager and this component;
    /// the world, rig, menus and HUD are all built here at runtime.
    public class NBGame : MonoBehaviour
    {
        public static NBGame Instance { get; private set; }
        public bool InMatch { get; private set; }

        NBRig m_Rig;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 90;
            QualitySettings.shadows = ShadowQuality.Disable;
            AudioListener.volume = NBSettings.Volume;
            NBAudio.Init();
            NBArena.Build();
            m_Rig = NBRig.Create(NBArena.LobbySpawn);
            NBPointer.Create(m_Rig);
            NBMenu.Create();
            NBHud.Create();
        }

        void Start()
        {
            NBInput.SetFloorOrigin();
            Invoke(nameof(FloorAgain), 1.5f);
            NBMenu.Instance.ShowLobby(NBSettings.SetupDone ? NBMenu.Tab.Nullbound : NBMenu.Tab.Setup);
        }

        void FloorAgain() => NBInput.SetFloorOrigin();

        public void OnLocalPlayerSpawned(NBPlayer p)
        {
            InMatch = true;
            NBMenu.Instance.Hide();
            NBHud.Instance.SetMatchVisible(true);
            NBHud.Instance.Big("FIGHT!", new Color(1f, 0.85f, 0.3f), 1.5f);
        }

        public void ReturnToLobby()
        {
            if (!InMatch && !NBMenu.Instance.Visible) { NBMenu.Instance.ShowLobby(NBMenu.Tab.Nullbound); return; }
            InMatch = false;
            NBHud.Instance.SetMatchVisible(false);
            m_Rig.SpeedMul = 1f;
            m_Rig.LocomotionLocked = false;
            m_Rig.Frozen = false;
            m_Rig.ClearStun();
            m_Rig.Teleport(NBArena.LobbySpawn, Vector3.forward);
            NBMenu.Instance.ShowLobby(NBMenu.Tab.Nullbound);
        }

        void Update()
        {
            if (NBInput.MenuToggle)
            {
                var menu = NBMenu.Instance;
                if (InMatch)
                {
                    if (menu.Visible) menu.Hide(); else menu.ShowInMatch();
                }
                else if (!menu.Visible) menu.ShowLobby(NBMenu.Tab.Nullbound);
            }

            // desktop testing: lock the mouse while fighting, free it for menus
            if (!NBInput.XRActive)
            {
                bool wantLock = InMatch && !NBMenu.Instance.Visible;
                var mode = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
                if (Cursor.lockState != mode) { Cursor.lockState = mode; Cursor.visible = !wantLock; }
            }
        }
    }
}
