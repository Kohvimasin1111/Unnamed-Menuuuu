using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace Unnamed_Free.Classes.Assets
{
    public class StartupSound : MonoBehaviour
    {
        private static bool played = false;

        public static void Play()
        {
            if (played) return;
            played = true;

            GameObject go = new GameObject("StartupSound");
            DontDestroyOnLoad(go);
            go.AddComponent<StartupSound>().StartCoroutine(go.GetComponent<StartupSound>().LoadAndPlay());
        }

        private IEnumerator LoadAndPlay()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("HaveFun.wav", StringComparison.OrdinalIgnoreCase));

            if (resName == null)
            {
                Debug.LogError("[StartupSound] HaveFun.wav not found - set Build Action to Embedded Resource");
                Destroy(gameObject);
                yield break;
            }

            // Unity can't load a wav straight from a stream, so write it to a temp file first
            string tempPath = Path.Combine(Path.GetTempPath(), "HaveFun_startup.wav");
            using (Stream s = asm.GetManifestResourceStream(resName))
            using (FileStream fs = File.Create(tempPath))
            {
                s.CopyTo(fs);
            }

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip("file:///" + tempPath, AudioType.WAV))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[StartupSound] Failed to load: " + req.error);
                    Destroy(gameObject);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 0f; // 2D sound, plays at full volume regardless of position
                src.volume = 1f;
                src.clip = clip;
                src.Play();

                yield return new WaitForSeconds(clip.length + 0.5f);
            }

            try { File.Delete(tempPath); } catch { }
            Destroy(gameObject);
        }
    }
}