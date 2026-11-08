using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StupidTemplate.ImGuiUI
{
    // Only looks at files embedded from Classes/Assets/Dll. Nothing is searched on disk.
    public static class ImGuiLoader
    {
        private static bool done;
        private static readonly Dictionary<string, Assembly> cache = new Dictionary<string, Assembly>();

        // Classes/Assets/Dll/cimgui.dll  ->  "<Namespace>.Classes.Assets.Dll.cimgui.dll"
        private const string Folder = ".Classes.Assets.Dll.";

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);

        public static void Init()
        {
            if (done)
                return;
            done = true;

            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;

            byte[] native = ReadEmbedded("cimgui.dll");
            if (native == null)
            {
                LogResources();
                throw new Exception("cimgui.dll not found in Classes/Assets/Dll (Build Action must be Embedded Resource)");
            }

            string target = WriteNative(native);

            if (LoadLibrary(target) == IntPtr.Zero)
                throw new Exception("LoadLibrary failed for " + target + " (error " + Marshal.GetLastWin32Error() + ")");

            Debug.Log("ImGuiLoader: cimgui.dll loaded from embedded Classes/Assets/Dll");
        }

        private static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;

            if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;

            Assembly found;
            if (cache.TryGetValue(name, out found))
                return found;

            byte[] data = ReadEmbedded(name + ".dll");
            if (data != null)
            {
                found = Assembly.Load(data);
                cache[name] = found;
                Debug.Log("ImGuiLoader: " + name + ".dll loaded from embedded Classes/Assets/Dll");
                return found;
            }

            // Not embedded. The game already contains System.Numerics.Vector2 somewhere,
            // so hand ImGui.NET that assembly instead of needing a separate file.
            if (name == "System.Numerics.Vectors")
            {
                found = typeof(System.Numerics.Vector2).Assembly;
                cache[name] = found;
                Debug.Log("ImGuiLoader: System.Numerics.Vectors redirected to " + found.GetName().Name);
                return found;
            }

            return null;
        }

        // Only matches files inside Classes/Assets/Dll
        private static byte[] ReadEmbedded(string fileName)
        {
            Assembly self = Assembly.GetExecutingAssembly();

            string match = self.GetManifestResourceNames().FirstOrDefault(n =>
                n.IndexOf(Folder, StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return null;

            using (Stream s = self.GetManifestResourceStream(match))
            {
                byte[] buffer = new byte[s.Length];
                int read = 0;
                while (read < buffer.Length)
                {
                    int n = s.Read(buffer, read, buffer.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
                return buffer;
            }
        }

        // cimgui.dll is native, so Windows needs it as a real file before LoadLibrary can load it.
        // It is written out of the embedded copy, you never place it yourself.
        private static string WriteNative(byte[] native)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string target = Path.Combine(folder, "cimgui.dll");

            if (File.Exists(target) && new FileInfo(target).Length == native.Length)
                return target;

            try
            {
                File.WriteAllBytes(target, native);
                return target;
            }
            catch
            {
                string temp = Path.Combine(Path.GetTempPath(), "imgui_native");
                Directory.CreateDirectory(temp);
                target = Path.Combine(temp, "cimgui.dll");

                if (!File.Exists(target) || new FileInfo(target).Length != native.Length)
                {
                    try { File.WriteAllBytes(target, native); }
                    catch (IOException) { }
                }
                return target;
            }
        }

        private static void LogResources()
        {
            foreach (string n in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                Debug.Log("ImGuiLoader resource: " + n);
        }
    }
}