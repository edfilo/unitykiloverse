using UnityEditor;
using UnityEngine;
using KiloWorld.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace KiloWorld.Rendering.Editor
{
    [CustomEditor(typeof(KiloWorldMasterProfile))]
    public class KiloWorldMasterProfileEditor : UnityEditor.Editor
    {
        private ShadowsMidtonesHighlights smhPreview;
        private UnityEditor.Editor smhEditor;

        public override void OnInspectorGUI()
        {
            // Draw default inspector first (hidden SMH fields stay hidden)
            DrawDefaultInspector();

            DrawWhiteBalance();
            DrawShadowsMidtonesHighlights();
        }

        private void DrawWhiteBalance()
        {
            KiloWorldMasterProfile profile = (KiloWorldMasterProfile)target;
            if (profile == null) return;
            var settings = profile.postFX;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("White Balance", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enabled", settings.whiteBalanceActive);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Toggle White Balance");
                settings.whiteBalanceActive = enabled;
                EditorUtility.SetDirty(profile);
            }

            using (new EditorGUI.DisabledScope(!settings.whiteBalanceActive))
            {
                DrawOverrideSlider(profile, "Temperature", ref settings.temperatureOverride, ref settings.temperature, -100f, 100f);
                DrawOverrideSlider(profile, "Tint", ref settings.tintOverride, ref settings.tint, -100f, 100f);
            }
        }

        private void DrawShadowsMidtonesHighlights()
        {
            KiloWorldMasterProfile profile = (KiloWorldMasterProfile)target;
            if (profile == null) return;

            EnsureSmhEditor();
            if (smhEditor == null || smhPreview == null) return;

            SyncSmhFromProfile(profile);

            smhEditor.serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            smhEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                smhEditor.serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(profile, "Update Shadows/Midtones/Highlights");
                SyncSmhToProfile(profile);
                EditorUtility.SetDirty(profile);
            }
        }

        private void DrawOverrideSlider(KiloWorldMasterProfile profile, string label, ref bool overrideState, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newOverride = EditorGUILayout.Toggle(overrideState, GUILayout.Width(18));
            EditorGUI.BeginDisabledGroup(!newOverride);
            float newValue = EditorGUILayout.Slider(label, value, min, max);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, $"Update {label}");
                overrideState = newOverride;
                value = newValue;
                EditorUtility.SetDirty(profile);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void EnsureSmhEditor()
        {
            if (smhPreview == null)
            {
                smhPreview = ScriptableObject.CreateInstance<ShadowsMidtonesHighlights>();
                smhPreview.name = "KiloSettingsSMHPreview";
                smhPreview.active = true;
            }

            if (smhEditor == null)
            {
                Type editorType = Type.GetType("UnityEditor.Rendering.Universal.ShadowsMidtonesHighlightsEditor, Unity.RenderPipelines.Universal.Editor");
                smhEditor = editorType != null
                    ? UnityEditor.Editor.CreateEditor(smhPreview, editorType)
                    : UnityEditor.Editor.CreateEditor(smhPreview);
            }
        }

        private void SyncSmhFromProfile(KiloWorldMasterProfile profile)
        {
            var settings = profile.postFX;
            smhPreview.shadows.value = settings.shadows;
            smhPreview.midtones.value = settings.midtones;
            smhPreview.highlights.value = settings.highlights;
            smhPreview.shadows.overrideState = settings.shadowsOverride;
            smhPreview.midtones.overrideState = settings.midtonesOverride;
            smhPreview.highlights.overrideState = settings.highlightsOverride;
            smhPreview.shadowsStart.value = settings.shadowsStart;
            smhPreview.shadowsEnd.value = settings.shadowsEnd;
            smhPreview.highlightsStart.value = settings.highlightsStart;
            smhPreview.highlightsEnd.value = settings.highlightsEnd;
            smhPreview.shadowsStart.overrideState = settings.shadowsStartOverride;
            smhPreview.shadowsEnd.overrideState = settings.shadowsEndOverride;
            smhPreview.highlightsStart.overrideState = settings.highlightsStartOverride;
            smhPreview.highlightsEnd.overrideState = settings.highlightsEndOverride;
        }

        private void SyncSmhToProfile(KiloWorldMasterProfile profile)
        {
            var settings = profile.postFX;
            settings.shadows = smhPreview.shadows.value;
            settings.midtones = smhPreview.midtones.value;
            settings.highlights = smhPreview.highlights.value;
            settings.shadowsOverride = smhPreview.shadows.overrideState;
            settings.midtonesOverride = smhPreview.midtones.overrideState;
            settings.highlightsOverride = smhPreview.highlights.overrideState;
            settings.shadowsStart = smhPreview.shadowsStart.value;
            settings.shadowsEnd = smhPreview.shadowsEnd.value;
            settings.highlightsStart = smhPreview.highlightsStart.value;
            settings.highlightsEnd = smhPreview.highlightsEnd.value;
            settings.shadowsStartOverride = smhPreview.shadowsStart.overrideState;
            settings.shadowsEndOverride = smhPreview.shadowsEnd.overrideState;
            settings.highlightsStartOverride = smhPreview.highlightsStart.overrideState;
            settings.highlightsEndOverride = smhPreview.highlightsEnd.overrideState;
        }

        private void OnDisable()
        {
            if (smhEditor != null)
            {
                DestroyImmediate(smhEditor);
                smhEditor = null;
            }

            if (smhPreview != null)
            {
                DestroyImmediate(smhPreview);
                smhPreview = null;
            }
        }
    }
}
