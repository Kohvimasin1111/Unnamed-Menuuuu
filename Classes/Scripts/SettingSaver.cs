using BepInEx;
using GorillaLocomotion;
using HarmonyLib;
using StupidTemplate.Classes;
using StupidTemplate.Menu;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static StupidTemplate.Menu.Buttons;

namespace Unnamed_Free.Classes.Scripts
{
    // Runs SettingSaver every frame without touching any other file
    [HarmonyPatch(typeof(GTPlayer), "LateUpdate")]
    internal static class SettingSaverRunner
    {
        public static void Postfix()
        {
            SettingSaver.Update();
        }
    }

    internal static class SettingSaver
    {
        // ---------- Config ----------
        private const string FileName = "MenuSettings.txt";   // saved in BepInEx/config
        private const float CheckInterval = 0.5f;             // how often it looks for changes (seconds)
        private const float MinSaveGap = 2f;                  // never write the file more often than this
        private const int MaxCyclePresses = 25;               // max presses when restoring a cycle button

        // Mods that should never be turned back on at startup (buttonText)
        private static readonly HashSet<string> DoNotRestore = new HashSet<string>
        {
            // "Some Mod",
        };

        // Fields whose name contains one of these are NOT saved (timers, runtime state, etc.). Add more if something misbehaves.
        private static readonly string[] IgnoreFieldNames =
        {
            "time", "timer", "delay", "cooldown", "last", "next", "frame", "locked", "target", "pagenumber",
            "currentcategory", "keyboardmenuopen", "dragging", "searchactive", "enabled", "velocity", "position"
        };

        // Whole classes that are never scanned
        private static readonly string[] IgnoreTypeNames = { "Main", "SettingSaver", "SettingSaverRunner", "PluginInfo" };

        // ---------- State ----------
        private static readonly HashSet<string> enabledMods = new HashSet<string>();
        private static readonly Dictionary<string, string> values = new Dictionary<string, string>();       // your own SetInt/GetInt values
        private static readonly Dictionary<string, string> savedFields = new Dictionary<string, string>();  // reflected fields
        private static readonly Dictionary<string, string> savedTexts = new Dictionary<string, string>();   // button label texts
        private static List<FieldInfo> fieldList;

        private static bool loaded;
        private static float nextCheck;
        private static float lastSave = -999f;
        private static string lastSignature = "";

        private static string FilePath
        {
            get { return Path.Combine(Paths.ConfigPath, FileName); }
        }

        // ---------- Called every frame by SettingSaverRunner ----------
        public static void Update()
        {
            try
            {
                if (buttons == null || buttons.Length == 0)
                    return;

                if (!loaded)
                {
                    loaded = true;
                    BuildFieldList();
                    Load();
                    lastSignature = BuildSignature();
                    nextCheck = Time.unscaledTime + CheckInterval;
                    return;
                }

                if (Time.unscaledTime < nextCheck)
                    return;

                nextCheck = Time.unscaledTime + CheckInterval;

                string signature = BuildSignature();
                if (signature != lastSignature && Time.unscaledTime - lastSave >= MinSaveGap)
                {
                    lastSignature = signature;
                    Save();
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver update failed: " + exc.Message);
            }
        }

        // ---------- Field scanning ----------
        private static void BuildFieldList()
        {
            fieldList = new List<FieldInfo>();

            Type[] types;
            try { types = typeof(SettingSaver).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            foreach (Type type in types)
            {
                try
                {
                    if (type.IsGenericType || IgnoreTypeNames.Contains(type.Name))
                        continue;

                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsLiteral || field.IsInitOnly)
                            continue;

                        Type ft = field.FieldType;
                        if (!(ft == typeof(bool) || ft == typeof(int) || ft == typeof(float) || ft == typeof(string) || ft.IsEnum))
                            continue;

                        string lower = field.Name.ToLowerInvariant();
                        if (IgnoreFieldNames.Any(n => lower.Contains(n)))
                            continue;

                        fieldList.Add(field);
                    }
                }
                catch { }
            }
        }

        private static string FieldKey(FieldInfo f)
        {
            return f.DeclaringType.FullName + "." + f.Name;
        }

        private static string FieldToString(FieldInfo f)
        {
            object v = f.GetValue(null);
            if (v == null) return null;
            if (v is bool) return (bool)v ? "1" : "0";
            if (v is float) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
            if (f.FieldType.IsEnum) return Convert.ToInt32(v).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static void StringToField(FieldInfo f, string s)
        {
            Type t = f.FieldType;
            if (t == typeof(bool)) f.SetValue(null, s == "1");
            else if (t == typeof(int)) f.SetValue(null, int.Parse(s, CultureInfo.InvariantCulture));
            else if (t == typeof(float)) f.SetValue(null, float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
            else if (t == typeof(string)) f.SetValue(null, s);
            else if (t.IsEnum) f.SetValue(null, Enum.ToObject(t, int.Parse(s, CultureInfo.InvariantCulture)));
        }

        // ---------- Change detection ----------
        private static string BuildSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Main.closeStyle).Append('|').Append(Main.clickSound).Append('|');

            foreach (ButtonInfo button in buttons.SelectMany(list => list))
            {
                if (button == null || string.IsNullOrEmpty(button.buttonText))
                    continue;

                if (button.isTogglable && button.enabled)
                    sb.Append(button.buttonText).Append(';');

                if (button.overlapText != null)
                    sb.Append(button.buttonText).Append('~').Append(button.overlapText).Append(';');
            }

            if (fieldList != null)
            {
                foreach (FieldInfo f in fieldList)
                {
                    try { sb.Append(FieldKey(f)).Append('=').Append(FieldToString(f)).Append(';'); }
                    catch { }
                }
            }

            foreach (KeyValuePair<string, string> pair in values.OrderBy(p => p.Key))
                sb.Append(pair.Key).Append('=').Append(pair.Value).Append(';');

            return sb.ToString();
        }

        // ---------- Your own values (optional) ----------
        public static void SetInt(string key, int value) { SetString(key, value.ToString(CultureInfo.InvariantCulture)); }
        public static void SetFloat(string key, float value) { SetString(key, value.ToString("R", CultureInfo.InvariantCulture)); }
        public static void SetBool(string key, bool value) { SetString(key, value ? "1" : "0"); }

        public static void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return;

            values[key] = value;
        }

        public static int GetInt(string key, int fallback = 0)
        {
            int result;
            string s;
            return values.TryGetValue(key, out s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        public static float GetFloat(string key, float fallback = 0f)
        {
            float result;
            string s;
            return values.TryGetValue(key, out s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        public static bool GetBool(string key, bool fallback = false)
        {
            string s;
            return values.TryGetValue(key, out s) ? s == "1" : fallback;
        }

        public static string GetString(string key, string fallback = "")
        {
            string s;
            return values.TryGetValue(key, out s) ? s : fallback;
        }

        // Deletes the save file and forgets everything (things that are on right now stay on)
        public static void Reset()
        {
            enabledMods.Clear();
            values.Clear();
            savedFields.Clear();
            savedTexts.Clear();

            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver reset failed: " + exc.Message);
            }

            lastSignature = BuildSignature();
        }

        // ---------- Saving ----------
        public static void Save()
        {
            lastSave = Time.unscaledTime;

            try
            {
                values["closeStyle"] = Main.closeStyle.ToString(CultureInfo.InvariantCulture);
                values["clickSound"] = Main.clickSound.ToString(CultureInfo.InvariantCulture);

                enabledMods.Clear();
                savedTexts.Clear();
                foreach (ButtonInfo button in buttons.SelectMany(list => list))
                {
                    if (button == null || string.IsNullOrEmpty(button.buttonText))
                        continue;

                    if (button.isTogglable && button.enabled)
                        enabledMods.Add(button.buttonText);

                    if (button.overlapText != null)
                        savedTexts[button.buttonText] = button.overlapText;
                }

                savedFields.Clear();
                if (fieldList != null)
                {
                    foreach (FieldInfo f in fieldList)
                    {
                        try
                        {
                            string s = FieldToString(f);
                            if (s != null)
                                savedFields[FieldKey(f)] = s;
                        }
                        catch { }
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Menu settings. mod: = enabled mod, text: = button label, field: = mod variable, set: = custom value");

                foreach (string mod in enabledMods.OrderBy(m => m))
                    sb.AppendLine("mod:" + Escape(mod));

                foreach (KeyValuePair<string, string> pair in savedTexts.OrderBy(p => p.Key))
                    sb.AppendLine("text:" + Escape(pair.Key) + "=" + Escape(pair.Value));

                foreach (KeyValuePair<string, string> pair in savedFields.OrderBy(p => p.Key))
                    sb.AppendLine("field:" + Escape(pair.Key) + "=" + Escape(pair.Value));

                foreach (KeyValuePair<string, string> pair in values.OrderBy(p => p.Key))
                    sb.AppendLine("set:" + Escape(pair.Key) + "=" + Escape(pair.Value));

                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver save failed: " + exc.Message);
            }
        }

        // ---------- Loading ----------
        public static void Load()
        {
            enabledMods.Clear();
            values.Clear();
            savedFields.Clear();
            savedTexts.Clear();

            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string rawLine in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string line = rawLine.Trim();
                        if (line.Length == 0 || line.StartsWith("#"))
                            continue;

                        if (line.StartsWith("mod:"))
                        {
                            enabledMods.Add(Unescape(line.Substring(4)));
                            continue;
                        }

                        Dictionary<string, string> target = null;
                        string body = null;

                        if (line.StartsWith("text:")) { target = savedTexts; body = line.Substring(5); }
                        else if (line.StartsWith("field:")) { target = savedFields; body = line.Substring(6); }
                        else if (line.StartsWith("set:")) { target = values; body = line.Substring(4); }

                        if (target != null)
                        {
                            int split = body.IndexOf('=');
                            if (split > 0)
                                target[Unescape(body.Substring(0, split))] = Unescape(body.Substring(split + 1));
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver load failed: " + exc.Message);
            }

            Apply();
        }

        private static void Apply()
        {
            // 1. Cycle buttons: press them until their label matches the saved one (fixes private variables too)
            try
            {
                foreach (ButtonInfo button in buttons.SelectMany(list => list))
                {
                    if (button == null || button.isTogglable || button.method == null || string.IsNullOrEmpty(button.buttonText))
                        continue;

                    string wanted;
                    if (!savedTexts.TryGetValue(button.buttonText, out wanted) || button.overlapText == wanted)
                        continue;

                    for (int i = 0; i < MaxCyclePresses && button.overlapText != wanted; i++)
                    {
                        string before = button.overlapText;
                        try { button.method.Invoke(); }
                        catch (Exception exc)
                        {
                            Debug.LogError("SettingSaver cycle failed for " + button.buttonText + ": " + exc.Message);
                            break;
                        }

                        if (button.overlapText == before)   // pressing doesn't change the label, so it is not a cycle button
                            break;
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver cycle restore failed: " + exc.Message);
            }

            // 2. Public variables (set after the cycling so the saved value always wins)
            try
            {
                if (fieldList != null)
                {
                    foreach (FieldInfo f in fieldList)
                    {
                        string s;
                        if (!savedFields.TryGetValue(FieldKey(f), out s))
                            continue;

                        try { StringToField(f, s); }
                        catch { }
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver field restore failed: " + exc.Message);
            }

            // 3. Menu settings and the labels on the buttons
            try
            {
                if (values.ContainsKey("closeStyle"))
                {
                    int style = GetInt("closeStyle", Main.closeStyle);
                    if (style >= 0 && style < Main.closeStyleNames.Length)
                        Main.closeStyle = style;
                }

                if (values.ContainsKey("clickSound"))
                {
                    int sound = GetInt("clickSound", Main.clickSound);
                    if (sound >= 0 && sound < Main.clickSoundNames.Length)
                        Main.clickSound = sound;
                }

                foreach (ButtonInfo button in buttons.SelectMany(list => list))
                {
                    string wanted;
                    if (button != null && button.buttonText != null && savedTexts.TryGetValue(button.buttonText, out wanted))
                        button.overlapText = wanted;
                }

                ButtonInfo closeButton = Main.GetIndex(Main.CloseStyleButtonText);
                if (closeButton != null)
                    closeButton.overlapText = "Close Style: " + Main.closeStyleNames[Main.closeStyle];

                ButtonInfo soundButton = Main.GetIndex(Main.ClickSoundButtonText);
                if (soundButton != null)
                    soundButton.overlapText = "Click Sound: " + Main.clickSoundNames[Main.clickSound];
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver apply failed: " + exc.Message);
            }

            // 4. Turn the saved mods back on
            try
            {
                foreach (ButtonInfo button in buttons.SelectMany(list => list))
                {
                    if (button == null || !button.isTogglable || string.IsNullOrEmpty(button.buttonText))
                        continue;

                    if (!enabledMods.Contains(button.buttonText) || DoNotRestore.Contains(button.buttonText) || button.enabled)
                        continue;

                    button.enabled = true;

                    if (button.enableMethod != null)
                    {
                        try { button.enableMethod.Invoke(); }
                        catch (Exception exc)
                        {
                            Debug.LogError("SettingSaver enableMethod failed for " + button.buttonText + ": " + exc.Message);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.LogError("SettingSaver mod restore failed: " + exc.Message);
            }
        }

        // ---------- Helpers ----------
        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("=", "\\e");
        }

        private static string Unescape(string s)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'e': sb.Append('='); break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}