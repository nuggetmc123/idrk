using Fusion;
using UnityEngine;

namespace Nullbound
{
    public enum MicMode { Open = 0, PushToTalk = 1, Muted = 2 }

    /// <summary>
    /// Voice chat, sent through Photon Fusion (no extra voice package needed).
    /// Lives on each fighter (bots stay quiet).
    ///
    /// Your side: records the headset mic at 16 kHz, only sends while you're talking
    /// (or holding the left stick in, in push-to-talk), squeezes it with <see cref="VoiceCodec"/> and sends
    /// 40 ms packets to the other players.
    /// Their side: a <see cref="JitterBuffer"/> smooths out the network and a 3D sound
    /// plays from the talker's head. A light shows above them while they talk.
    ///
    /// Menu > Settings: open mic / push-to-talk / muted, and voice volume.
    /// </summary>
    public class VoiceChat : NetworkBehaviour
    {
        /// <summary>Your own voice chat (for the menu's level meter and mic test).</summary>
        public static VoiceChat Local { get; private set; }

        /// <summary>Mic test: hear your own voice back through the whole send/receive path.</summary>
        public static bool MicTest;

        /// <summary>How loud your mic is right now (0..1), for the menu's meter.</summary>
        public float MicLevel { get; private set; }
        public bool Sending { get; private set; }
        public string MicStatus { get; private set; } = "";

        Fighter fighter;
        AudioSource speaker;
        JitterBuffer jitter;
        VoiceReceiver receiver;
        int sendSeq;
        float lastHeard = -10f;
        Transform talkLight;

        // Microphone capture.
        AudioClip micClip;
        string micDevice;
        int micReadPos;
        float[] micChunk = new float[0];
        readonly float[] packet = new float[VoiceCodec.PacketSamples];
        int packetFill;
        readonly NoiseGate gate = new NoiseGate();
        float retryMicAt;

        public override void Spawned()
        {
            fighter = GetComponent<Fighter>();
            jitter = new JitterBuffer();
            receiver = new VoiceReceiver(jitter);

            var go = new GameObject("Voice");
            go.transform.SetParent(transform, false);
            speaker = go.AddComponent<AudioSource>();
            speaker.clip = AudioClip.Create("Voice", VoiceCodec.SampleRate, 1, VoiceCodec.SampleRate, true, OnAudioRead);
            speaker.loop = true;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
            speaker.minDistance = 2f;
            speaker.maxDistance = 35f;
            speaker.dopplerLevel = 0f;
            speaker.priority = 10;
            speaker.Play();

            talkLight = NBUI.Part(go.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.09f, NBUI.Glow(new Color(0.3f, 1f, 0.45f)), default(Vector3), "TalkLight");
            talkLight.gameObject.SetActive(false);

            if (HasStateAuthority && fighter != null && !fighter.IsBot) Local = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopMic();
            if (Local == this) Local = null;
        }

        void OnAudioRead(float[] data) => jitter.Read(data, data.Length);

        void OnDestroy() => StopMic();

        void Update()
        {
            if (speaker == null) return;
            if (fighter != null) speaker.transform.position = fighter.HeadPos;
            receiver.Tick(Time.unscaledTimeAsDouble);
            speaker.volume = GameSettings.VoiceVolume;

            if (HasStateAuthority)
            {
                if (fighter != null && fighter.IsBot) return;
                // Your own voice only plays back during Mic Test (and then "in your head", not from your body).
                speaker.spatialBlend = MicTest ? 0f : 1f;
                Capture();
            }
            else
            {
                bool talking = Time.time - lastHeard < 0.25f;
                if (fighter != null && fighter.Ghost) talking = false; // an invisible mod stays invisible
                talkLight.gameObject.SetActive(talking);
                if (talking) talkLight.position = speaker.transform.position + Vector3.up * 0.75f;
            }
        }

        // ---------------- your microphone

        void Capture()
        {
            var mode = GameSettings.Mic;
            if (mode == MicMode.Muted || Moderation.Muted)
            {
                StopMic();
                MicStatus = Moderation.Muted ? "Muted by a moderator" : "Muted";
                MicLevel = 0f;
                Sending = false;
                return;
            }
            if (micClip == null && !StartMic()) { MicLevel = 0f; Sending = false; return; }

            int pos = Microphone.GetPosition(micDevice);
            int length = micClip.samples;
            int available = (pos - micReadPos + length) % length;
            if (available <= 0) return;
            if (micChunk.Length < available) micChunk = new float[length];
            ReadMic(micReadPos, available, length);
            micReadPos = pos;

            bool pushHeld = mode == MicMode.Open || FightInput.TalkHeld;
            for (int i = 0; i < available; i++)
            {
                packet[packetFill++] = Mathf.Clamp(micChunk[i] * 1.6f, -1f, 1f); // a little boost; Quest mics are quiet
                if (packetFill < VoiceCodec.PacketSamples) continue;
                packetFill = 0;

                float rms = VoiceCodec.Rms(packet, 0, VoiceCodec.PacketSamples);
                MicLevel = Mathf.Max(rms * 6f, MicLevel * 0.8f);
                bool talking = gate.Open(rms) && pushHeld;
                Sending = talking;
                if (!talking && !MicTest) continue;

                var bytes = VoiceCodec.Encode(packet, 0, sendSeq);
                sendSeq = (sendSeq + 1) & 0xFFFF;
                if (talking) RPC_Voice(bytes);
                if (MicTest) Hear(bytes);
            }
            MicStatus = mode == MicMode.PushToTalk ? (FightInput.TalkHeld ? "Talking (holding the left stick in)" : "Hold the left stick in to talk") : Sending ? "Talking" : "Open mic";
        }

        void ReadMic(int start, int count, int length)
        {
            // The mic writes into a looping buffer: read in up to two parts when it wraps round.
            int first = Mathf.Min(count, length - start);
            var a = new float[first];
            micClip.GetData(a, start);
            System.Array.Copy(a, 0, micChunk, 0, first);
            if (count > first)
            {
                var b = new float[count - first];
                micClip.GetData(b, 0);
                System.Array.Copy(b, 0, micChunk, first, count - first);
            }
        }

        bool StartMic()
        {
            if (Time.time < retryMicAt) return false;
            retryMicAt = Time.time + 3f;
            if (!MicPermission.Granted)
            {
                MicPermission.Request(); // only pops up if it wasn't asked at startup
                MicStatus = "Mic not allowed. Quest Settings > Apps > Nullbound > Permissions > Microphone";
                return false;
            }
            if (Microphone.devices.Length == 0)
            {
                MicStatus = "No microphone found";
                return false;
            }
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 1, VoiceCodec.SampleRate);
            if (micClip == null)
            {
                MicStatus = "Couldn't start the microphone";
                return false;
            }
            micReadPos = 0;
            packetFill = 0;
            MicStatus = "Open mic";
            return true;
        }

        void StopMic()
        {
            if (micClip == null) return;
            Microphone.End(micDevice);
            micClip = null;
        }

        // ---------------- network

        /// <summary>40 ms of squeezed voice for everyone else. Unreliable: a late voice packet is useless anyway.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
        public void RPC_Voice(byte[] data)
        {
            if (HasStateAuthority) return; // (playing solo, the offline stand-in calls this directly)
            Hear(data);
        }

        void Hear(byte[] data)
        {
            receiver.Receive(data, Time.unscaledTimeAsDouble);
            lastHeard = Time.time;
        }
    }
}
