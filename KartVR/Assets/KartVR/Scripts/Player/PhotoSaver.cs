using System;
using System.IO;
using UnityEngine;

namespace KartVR
{
    /// <summary>
    /// Saves Quest Cam photos where you'll find them:
    /// - Quest (Android): into the headset's shared Pictures/KartVR folder through Android's MediaStore, so they
    ///   show up with your other photos (Files app / media, or on your phone in the Meta Horizon app's media sync).
    /// - PC: your Pictures/KartVR folder.
    /// Returns where it went, or null if it failed.
    /// </summary>
    public static class PhotoSaver
    {
        public static string Save(byte[] jpg, string name)
        {
            try
            {
                if (Application.platform == RuntimePlatform.Android) return SaveAndroid(jpg, name);
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "KartVR");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name + ".jpg");
                File.WriteAllBytes(path, jpg);
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KartVR] Saving the photo to the gallery failed (" + e.Message + "); keeping it in the app's own folder.");
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, name + ".jpg");
                    File.WriteAllBytes(path, jpg);
                    return path;
                }
                catch (Exception e2)
                {
                    Debug.LogException(e2);
                    return null;
                }
            }
        }

        /// <summary>Android 10+ way to add a picture to the shared gallery (no storage permission needed).</summary>
        static string SaveAndroid(byte[] jpg, string name)
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var values = new AndroidJavaObject("android.content.ContentValues"))
            using (var images = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            using (var collection = images.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            {
                values.Call("put", "_display_name", name + ".jpg");
                values.Call("put", "mime_type", "image/jpeg");
                values.Call("put", "relative_path", "Pictures/KartVR");
                using (var item = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (item == null) throw new Exception("MediaStore insert returned nothing");
                    using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                    {
                        stream.Call("write", (sbyte[])(Array)jpg);
                        stream.Call("flush");
                        stream.Call("close");
                    }
                }
            }
            return "Pictures/KartVR/" + name + ".jpg";
        }
    }
}
