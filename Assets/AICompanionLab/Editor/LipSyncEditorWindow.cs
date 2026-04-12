#if UNITY_EDITOR
using System.Collections.Generic;
using Nyxara.AICompanion.LipSync;
using UnityEditor;
using UnityEngine;

namespace Nyxara.AICompanion.Editor
{
    public class LipSyncEditorWindow : EditorWindow
    {
        private SkinnedMeshRenderer _targetRenderer;
        private LipSyncData _lipSyncData;
        private Vector2 _scrollPosition;
        private string _testPhrase = "Hello, how are you today?";

        [MenuItem("Nyxara/AI Companion/Lip Sync Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LipSyncEditorWindow>("Lip Sync Editor");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lip Sync Configuration", EditorStyles.boldLabel);
            _targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Renderer", _targetRenderer, typeof(SkinnedMeshRenderer), true);
            _lipSyncData = (LipSyncData)EditorGUILayout.ObjectField("Lip Sync Data", _lipSyncData, typeof(LipSyncData), false);

            if (_lipSyncData == null)
            {
                if (GUILayout.Button("Create New Lip Sync Data"))
                {
                    CreateLipSyncData();
                }

                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Viseme to Blendshape Mapping", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(320));
            for (var i = 0; i < _lipSyncData.visemeMappings.Count; i++)
            {
                var mapping = _lipSyncData.visemeMappings[i];
                EditorGUILayout.BeginHorizontal("box");
                mapping.viseme = (Viseme)EditorGUILayout.EnumPopup(mapping.viseme, GUILayout.Width(100));
                mapping.blendshapeName = EditorGUILayout.TextField(mapping.blendshapeName);
                mapping.intensity = EditorGUILayout.Slider(mapping.intensity, 0f, 100f, GUILayout.Width(120));
                mapping.jawOpenContribution = EditorGUILayout.Slider(mapping.jawOpenContribution, 0f, 1f, GUILayout.Width(120));

                if (GUILayout.Button("Test", GUILayout.Width(50)))
                {
                    TestViseme(mapping);
                }

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _lipSyncData.visemeMappings.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Viseme Mapping"))
            {
                _lipSyncData.visemeMappings.Add(new VisemeMapping { viseme = Viseme.sil, blendshapeName = "", intensity = 0f });
                EditorUtility.SetDirty(_lipSyncData);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            _lipSyncData.smoothTime = EditorGUILayout.Slider("Smooth Time", _lipSyncData.smoothTime, 0.01f, 0.3f);
            _lipSyncData.jawOpenMultiplier = EditorGUILayout.Slider("Jaw Open Multiplier", _lipSyncData.jawOpenMultiplier, 0f, 1f);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            _testPhrase = EditorGUILayout.TextField("Test Phrase", _testPhrase);
            if (GUILayout.Button("Test Lip Sync"))
            {
                TestLipSync();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_lipSyncData);
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateLipSyncData()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Lip Sync Data", "LipSyncData.asset", "asset", "Choose location");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var data = CreateInstance<LipSyncData>();
            data.visemeMappings = new List<VisemeMapping>
            {
                new() { viseme = Viseme.AA, blendshapeName = "mouthAH", intensity = 80f },
                new() { viseme = Viseme.IY, blendshapeName = "mouthSmile", intensity = 70f },
                new() { viseme = Viseme.UH, blendshapeName = "mouthFunnel", intensity = 75f },
                new() { viseme = Viseme.OW, blendshapeName = "mouthO", intensity = 80f },
                new() { viseme = Viseme.EH, blendshapeName = "mouthDimple", intensity = 65f },
                new() { viseme = Viseme.FV, blendshapeName = "mouthPress", intensity = 50f },
                new() { viseme = Viseme.M, blendshapeName = "mouthClose", intensity = 60f },
                new() { viseme = Viseme.sil, blendshapeName = "mouthRest", intensity = 0f }
            };

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            _lipSyncData = data;
            EditorGUIUtility.PingObject(data);
        }

        private void TestViseme(VisemeMapping mapping)
        {
            if (_targetRenderer == null || _targetRenderer.sharedMesh == null)
            {
                Debug.LogWarning("No face renderer selected");
                return;
            }

            for (var i = 0; i < _targetRenderer.sharedMesh.blendShapeCount; i++)
            {
                _targetRenderer.SetBlendShapeWeight(i, 0f);
            }

            var index = _targetRenderer.sharedMesh.GetBlendShapeIndex(mapping.blendshapeName);
            if (index >= 0)
            {
                _targetRenderer.SetBlendShapeWeight(index, mapping.intensity);
            }
        }

        private async void TestLipSync()
        {
            var controller = FindFirstObjectByType<VisemeLipSyncController>();
            if (controller != null)
            {
                await controller.SpeakWithLipSync(_testPhrase);
            }
            else
            {
                Debug.LogWarning("No VisemeLipSyncController found in scene");
            }
        }
    }
}
#endif
