
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Profiling;

namespace AV.Core
{
    public class UpdateLoopHook : MonoBehaviour
    {
        public static Action onBeforeFixedUpdate;
        
        
        private static void RecursivePlayerLoopPrint(PlayerLoopSystem loop, StringBuilder sb, int depth)
        {
            if (depth == 0)
                sb.AppendLine("ROOT NODE");
            else if (loop.type != null)
            {
                for (int i = 0; i < depth; i++)
                    sb.Append("\t");
                sb.AppendLine(loop.type.Name);
            }
            if (loop.subSystemList != null)
            {
                depth++;
                foreach (var s in loop.subSystemList)
                    RecursivePlayerLoopPrint(s, sb, depth);
            }
        }
        
        static void LogAllSystems(PlayerLoopSystem loop)
        {
            var sb = new StringBuilder();
            RecursivePlayerLoopPrint(loop, sb, 0);
            Debug.Log(sb.ToString());
        }
        
        static bool hooked;
        
#if UNITY_EDITOR
        private void Awake()
        {
            onBeforeFixedUpdate = null;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingPlayMode)
                    onBeforeFixedUpdate = null;
            };
        }
        #endif
        void OnEnable()
        {
            AppStart();
        }
        
        #if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(System))]
        class SystemDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUIUtility.labelWidth += 100;
                EditorGUI.BeginProperty(position, label, property);
                var enabled = property.FindPropertyRelative("enabled");
                label.text = property.FindPropertyRelative("type").stringValue;
                EditorGUI.PropertyField(position, enabled, label);
                EditorGUI.EndProperty();
            }
        }
        #endif
        [Serializable] public class System
        {
            public string type;
            public bool enabled = true;
            public System(PlayerLoopSystem s)
            {
                type = s.type.Name;
            }
        }
        [Serializable] public class SystemGroup : System
        {
            public System[] subSystems;
            public SystemGroup(PlayerLoopSystem s) : base(s)
            {
                type = s.type.Name;
                var sub = s.subSystemList;
                if (sub == null) return;
                subSystems = new System[sub.Length];
                for (int i = 0; i < sub.Length; i++)
                    subSystems[i] = new System(sub[i]);
            }
        }
        public List<SystemGroup> systemGroups;
        public List<System> systems;

        void AppStart()
        {
            onBeforeFixedUpdate = null;
            if (hooked)
                return;
            
            var defaultLoop = PlayerLoop.GetDefaultPlayerLoop();
            //LogAllSystems(defaultLoop);
            
            var disabledSystems = new HashSet<string>();
            foreach (var s in systemGroups)
            {
                if (!s.enabled) disabledSystems.Add(s.type);
                foreach (var sub in s.subSystems)
                    if (!sub.enabled) disabledSystems.Add(sub.type);
            }

            systemGroups.Clear();
            foreach (var s in defaultLoop.subSystemList)
                systemGroups.Add(new SystemGroup(s));
            
            foreach (var s in systemGroups)
            {
                if (disabledSystems.Contains(s.type)) s.enabled = false;
                foreach (var sub in s.subSystems)
                    if (disabledSystems.Contains(sub.type)) sub.enabled = false;
            }
            
            IterateAllSystemsRecursive(ref defaultLoop, (ref PlayerLoopSystem s) =>
            {
                if (s.type == null)
                    return;
                
                var disabled = disabledSystems.Contains(s.type.Name);
                
                if (disabled)
                    s = default;
            });
            
            PlayerLoop.SetPlayerLoop(defaultLoop);
        }
        
        delegate void LoopSystemRef(ref PlayerLoopSystem item);
        
        private static void IterateAllSystemsRecursive(ref PlayerLoopSystem system, LoopSystemRef action)
        {
            action(ref system);
            
            var sub = system.subSystemList;
            if (sub == null) return;
            
            for (var i = 0; i < sub.Length; i++)
                IterateAllSystemsRecursive(ref sub[i], action);
        }
    }
}