using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StupidTemplate.Notifications;
using UnityEngine;

namespace StupidTemplate.Classes
{
    public class AssetLoader
    {
        private const float Volume = 0.5f;   // 0 = silent, 1 = full

        private static AudioSource audioSource;
        private static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        // Plays a wav that is embedded in the dll, e.g. AssetLoader.PlayClick("menuopen.wav")
        public static void PlayClick(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            try
            {
                AudioClip clip = LoadClip(name);
                if (clip == null)
                    return;

                if (audioSource == null)
                {
                    GameObject audioObject = new GameObject("AssetLoaderAudio");
                    UnityEngine.Object.DontDestroyOnLoad(audioObject);
                    audioSource = audioObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 0f;
                    audioSource.playOnAwake = false;
                }

                audioSource.PlayOneShot(clip, Volume);
            }
            catch (Exception exc)
            {
                Fail(name, "could not play: " + exc.Message);
            }
        }

        // Returns the raw bytes of an embedded file, or null if it is not found
        public static byte[] GetEmbeddedResourceBytes(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        // Shows the problem in the log and in the menu notifications (once per file, the result is cached)
        private static void Fail(string name, string reason)
        {
            Debug.LogError("AssetLoader // " + name + " " + reason);

            try
            {
                NotifiLib.SendNotification("<color=grey>[</color><color=red>SOUND</color><color=grey>]</color> " + name + " " + reason);
            }
            catch { }
        }

        // Loads the wav once, then keeps it cached
        private static AudioClip LoadClip(string name)
        {
            if (clipCache.TryGetValue(name, out AudioClip cached))
                return cached;

            AudioClip clip = null;
            byte[] bytes = GetEmbeddedResourceBytes(name);

            if (bytes == null)
                Fail(name, "is not embedded (set Build Action to Embedded Resource)");
            else
                clip = WavToClip(name, bytes);

            clipCache[name] = clip;
            return clip;
        }

        // Turns a 16-bit PCM wav into a Unity AudioClip
        private static AudioClip WavToClip(string name, byte[] b)
        {
            int channels = 0, rate = 0, bits = 0, dataStart = -1, dataSize = 0;
            int pos = 12; // skip "RIFF", size, "WAVE"

            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;

                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToInt16(b, body + 14);
                }
                else if (id == "data")
                {
                    dataStart = body;
                    dataSize = Mathf.Min(size, b.Length - body);
                    break;
                }

                pos = body + size + (size & 1);
            }

            if (dataStart < 0 || channels <= 0 || bits != 16)
            {
                Fail(name, "must be a 16-bit PCM wav");
                return null;
            }

            int sampleCount = dataSize / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt16(b, dataStart + i * 2) / 32768f;

            AudioClip clip = AudioClip.Create(name, sampleCount / channels, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}