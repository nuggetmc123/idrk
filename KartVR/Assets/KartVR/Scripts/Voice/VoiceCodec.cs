using System;
using System.Collections.Generic;

namespace KartVR
{
    /// <summary>
    /// Squeezes voice down for sending over the network, and back.
    /// 16 kHz mono, IMA ADPCM (4 bits per sample) = 8 KB/s per talking player.
    /// Every packet carries its own decoder state, so a lost packet is just a short
    /// gap and never garbles what comes after it.
    ///
    /// Plain C# (no Unity), so it can be tested on its own.
    /// </summary>
    public static class VoiceCodec
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 320;            // 20 ms
        public const int FramesPerPacket = 2;           // 40 ms per packet
        public const int PacketSamples = FrameSamples * FramesPerPacket;
        public const int HeaderBytes = 5;               // sequence number (2) + predictor (2) + step index (1)
        public const int PacketBytes = HeaderBytes + PacketSamples / 2;

        static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060,
            1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
            7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
        };

        /// <summary>Encode one packet (PacketSamples floats in -1..1) into PacketBytes bytes. seq numbers the packets in order.</summary>
        public static byte[] Encode(float[] pcm, int offset, int seq)
        {
            var output = new byte[PacketBytes];
            int predictor = ToShort(pcm[offset]);
            int index = 0;
            // Pick a starting step size that suits how loud this packet is.
            int peak = 0;
            for (int i = 0; i < PacketSamples; i++) peak = Math.Max(peak, Math.Abs(ToShort(pcm[offset + i]) - (i > 0 ? ToShort(pcm[offset + i - 1]) : predictor)));
            while (index < 88 && StepTable[index] < peak / 4) index++;

            output[0] = (byte)(seq & 0xFF);
            output[1] = (byte)((seq >> 8) & 0xFF);
            output[2] = (byte)(predictor & 0xFF);
            output[3] = (byte)((predictor >> 8) & 0xFF);
            output[4] = (byte)index;

            for (int i = 0; i < PacketSamples; i++)
            {
                int sample = ToShort(pcm[offset + i]);
                int step = StepTable[index];
                int diff = sample - predictor;
                int code = 0;
                if (diff < 0) { code = 8; diff = -diff; }
                int delta = step >> 3;
                if (diff >= step) { code |= 4; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 2; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 1; delta += step; }
                predictor += (code & 8) != 0 ? -delta : delta;
                predictor = Clamp(predictor, -32768, 32767);
                index = Clamp(index + IndexTable[code], 0, 88);

                int b = HeaderBytes + i / 2;
                if ((i & 1) == 0) output[b] = (byte)code;
                else output[b] |= (byte)(code << 4);
            }
            return output;
        }

        /// <summary>Decode one packet into PacketSamples floats. Returns false if the data is the wrong size.</summary>
        public static bool Decode(byte[] data, float[] output, out int seq)
        {
            seq = 0;
            if (data == null || data.Length != PacketBytes || output.Length < PacketSamples) return false;
            seq = data[0] | (data[1] << 8);
            int predictor = (short)(data[2] | (data[3] << 8));
            int index = Clamp(data[4], 0, 88);
            for (int i = 0; i < PacketSamples; i++)
            {
                int b = data[HeaderBytes + i / 2];
                int code = (i & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
                int step = StepTable[index];
                int delta = step >> 3;
                if ((code & 4) != 0) delta += step;
                if ((code & 2) != 0) delta += step >> 1;
                if ((code & 1) != 0) delta += step >> 2;
                predictor += (code & 8) != 0 ? -delta : delta;
                predictor = Clamp(predictor, -32768, 32767);
                index = Clamp(index + IndexTable[code], 0, 88);
                output[i] = predictor / 32768f;
            }
            return true;
        }

        /// <summary>How loud a block of audio is (root mean square, 0..1).</summary>
        public static float Rms(float[] pcm, int offset, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++) sum += pcm[offset + i] * pcm[offset + i];
            return (float)Math.Sqrt(sum / Math.Max(1, count));
        }

        static int ToShort(float v) => Clamp((int)(v * 32767f), -32768, 32767);
        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>
    /// Puts arriving packets back in order before they're played (the internet can deliver them
    /// shuffled), and covers a lost packet with a faded copy of the one before, so the voice keeps
    /// its timing instead of stuttering. Call Receive when a packet arrives and Tick every frame.
    /// </summary>
    public class VoiceReceiver
    {
        readonly JitterBuffer output;
        readonly Dictionary<int, float[]> waiting = new Dictionary<int, float[]>();
        readonly Dictionary<int, double> arrivedAt = new Dictionary<int, double>();
        readonly float[] last = new float[VoiceCodec.PacketSamples];
        readonly float[] fill = new float[VoiceCodec.PacketSamples];
        int expected = -1;
        double lastArrival = -10;
        public int Lost, Late, Played;

        /// <summary>How long to wait for a missing packet before giving up on it.</summary>
        public double WaitForMissing = 0.06;

        public VoiceReceiver(JitterBuffer output) { this.output = output; }

        static int Diff(int a, int b) => (short)(a - b); // sequence numbers wrap at 65536

        public void Receive(byte[] data, double now)
        {
            var pcm = new float[VoiceCodec.PacketSamples];
            if (!VoiceCodec.Decode(data, pcm, out int seq)) return;
            // A new burst of talking (after a pause) or a big jump: start fresh from this packet.
            if (expected < 0 || now - lastArrival > 0.5 || Math.Abs(Diff(seq, expected)) > 25)
            {
                waiting.Clear();
                arrivedAt.Clear();
                expected = seq;
                Array.Clear(last, 0, last.Length);
            }
            lastArrival = now;
            if (Diff(seq, expected) < 0) { Late++; return; } // too late, its moment already played
            waiting[seq] = pcm;
            arrivedAt[seq] = now;
            Tick(now);
        }

        public void Tick(double now)
        {
            while (expected >= 0)
            {
                if (waiting.TryGetValue(expected, out var pcm))
                {
                    output.Push(pcm, pcm.Length);
                    Array.Copy(pcm, last, last.Length);
                    waiting.Remove(expected);
                    arrivedAt.Remove(expected);
                    Played++;
                    expected = (expected + 1) & 0xFFFF;
                    continue;
                }
                if (waiting.Count == 0) break;
                // Newer packets are here but this one isn't: wait a moment, then call it lost.
                double oldest = double.MaxValue;
                foreach (var t in arrivedAt.Values) oldest = Math.Min(oldest, t);
                if (waiting.Count < 2 && now - oldest < WaitForMissing) break;
                for (int i = 0; i < fill.Length; i++) fill[i] = last[i] * 0.5f * (1f - i / (float)fill.Length);
                output.Push(fill, fill.Length);
                Array.Clear(last, 0, last.Length);
                Lost++;
                expected = (expected + 1) & 0xFFFF;
            }
        }
    }

    /// <summary>
    /// Only sends while you're actually talking: opens when the mic gets louder than the
    /// threshold and stays open a little after you stop, so word endings aren't cut off.
    /// </summary>
    public class NoiseGate
    {
        public float threshold = 0.012f;
        public int holdPackets = 8; // ~320 ms
        int hold;

        public bool Open(float rms)
        {
            if (rms >= threshold) hold = holdPackets;
            else if (hold > 0) hold--;
            return hold > 0;
        }
    }

    /// <summary>
    /// Smooths out network hiccups on the listening side. Packets go in as they arrive; the
    /// speaker reads a steady stream. It waits until ~100 ms is stored before it starts playing,
    /// fades out instead of clicking if it runs dry, and skips ahead if it falls too far behind.
    /// Thread-safe (Unity reads it from the audio thread).
    /// </summary>
    public class JitterBuffer
    {
        readonly float[] ring;
        int read, write, count;
        bool playing;
        float last;
        readonly object gate = new object();

        public int StartSamples = VoiceCodec.SampleRate * 3 / 25;  // 120 ms
        public int MaxSamples = VoiceCodec.SampleRate * 2 / 5;     // 400 ms
        public int Buffered { get { lock (gate) return count; } }

        public JitterBuffer(int capacity = VoiceCodec.SampleRate) { ring = new float[capacity]; }

        public void Push(float[] samples, int n)
        {
            lock (gate)
            {
                for (int i = 0; i < n; i++)
                {
                    ring[write] = samples[i];
                    write = (write + 1) % ring.Length;
                    if (count < ring.Length) count++;
                    else read = (read + 1) % ring.Length;
                }
                // Too much delay piled up: drop the oldest audio so the voice stays live.
                while (count > MaxSamples) { read = (read + 1) % ring.Length; count--; }
            }
        }

        public void Read(float[] output, int n)
        {
            lock (gate)
            {
                if (!playing && count >= StartSamples) playing = true;
                for (int i = 0; i < n; i++)
                {
                    if (playing && count > 0)
                    {
                        last = ring[read];
                        read = (read + 1) % ring.Length;
                        count--;
                        output[i] = last;
                    }
                    else
                    {
                        // Ran dry: fade the last sample to silence (no click) and wait to refill.
                        playing = false;
                        last *= 0.95f;
                        output[i] = last;
                    }
                }
            }
        }
    }
}
