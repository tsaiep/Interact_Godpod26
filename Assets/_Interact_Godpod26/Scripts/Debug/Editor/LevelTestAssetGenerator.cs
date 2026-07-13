using System.IO;
using System.Reflection;
using RFIDBaggage.Levels;
using RFIDBaggage.Video;
using UnityEditor;
using UnityEngine;

namespace RFIDBaggage.DebugTools.Editor
{
    public static class LevelTestAssetGenerator
    {
        private const string LevelFolder = "Assets/_Interact_Godpod26/Data/Levels";
        private const string DatabasePath = "Assets/_Interact_Godpod26/Data/Levels/LevelDatabase.asset";
        private const string SystemFolder = "Assets/_Interact_Godpod26/Data/System";
        private const string VideoSystemConfigPath = "Assets/_Interact_Godpod26/Data/System/VideoSystemConfig.asset";
        private const string RenderTextureFolder = "Assets/_Interact_Godpod26/RenderTextures";
        private const string IdleRenderTexturePath = "Assets/_Interact_Godpod26/RenderTextures/RT_IdleVideo.renderTexture";
        private const string ContentRenderTexturePath = "Assets/_Interact_Godpod26/RenderTextures/RT_ContentVideo.renderTexture";

        [MenuItem("RFID Baggage/Create Phase 1 Test Level Assets")]
        public static void CreatePhaseOneAssets()
        {
            EnsureFolder("Assets/_Interact_Godpod26");
            EnsureFolder("Assets/_Interact_Godpod26/Data");
            EnsureFolder(LevelFolder);
            EnsureFolder(SystemFolder);
            EnsureFolder(RenderTextureFolder);

            LevelConfig[] levels = new LevelConfig[6];
            for (int i = 0; i < levels.Length; i++)
            {
                int number = i + 1;
                string levelPath = $"{LevelFolder}/Level_{number:00}.asset";
                LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPath);

                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelConfig>();
                    AssetDatabase.CreateAsset(level, levelPath);
                }

                ConfigureLevel(level, number);
                EditorUtility.SetDirty(level);
                levels[i] = level;
            }

            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            SerializedObject serializedDatabase = new SerializedObject(database);
            SerializedProperty levelsProperty = serializedDatabase.FindProperty("levels");
            levelsProperty.arraySize = levels.Length;

            for (int i = 0; i < levels.Length; i++)
            {
                levelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            }

            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);

            VideoSystemConfig videoSystemConfig = AssetDatabase.LoadAssetAtPath<VideoSystemConfig>(VideoSystemConfigPath);
            if (videoSystemConfig == null)
            {
                videoSystemConfig = ScriptableObject.CreateInstance<VideoSystemConfig>();
                AssetDatabase.CreateAsset(videoSystemConfig, VideoSystemConfigPath);
                EditorUtility.SetDirty(videoSystemConfig);
            }

            CreateRenderTextureIfMissing(IdleRenderTexturePath);
            CreateRenderTextureIfMissing(ContentRenderTexturePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RFID Baggage] Test LevelConfig assets, LevelDatabase, VideoSystemConfig, and RenderTextures created.");
        }

        private static void ConfigureLevel(LevelConfig level, int number)
        {
            string levelId = $"Level_{number:00}";
            string rfidId = $"RFID_{number:00}";
            string displayName = $"Test Level {number:00}";
            string folder = $"Level{number:00}";

            SetPrivateField(level, "levelId", levelId);
            SetPrivateField(level, "displayName", displayName);
            SetPrivateField(level, "rfidId", rfidId);
            SetPrivateField(level, "introVideoRelativePath", $"Videos/{folder}/{folder}_Intro.mp4");
            SetPrivateField(level, "successVideoRelativePath", $"Videos/{folder}/{folder}_Success.mp4");
            SetPrivateField(level, "failureVideoRelativePath", $"Videos/{folder}/{folder}_Failure.mp4");
            SetPrivateField(level, "finalFrameImageRelativePath", $"Images/{folder}/{folder}_FinalFrame.png");
            SetPrivateField(level, "gameplayDuration", 15f);
            SetPrivateField(level, "warningStartTime", 5f);
            SetPrivateField(level, "selectionInputCooldown", 0.15f);
            SetPrivateField(level, "confirmInputCooldown", 0.25f);
            SetPrivateField(level, "inputCooldown", 0.2f);
            SetPrivateField(level, "wrongSelectionDeductsTime", false);
            SetPrivateField(level, "wrongSelectionTimePenalty", 0f);
        }

        private static void SetPrivateField<T>(LevelConfig level, string fieldName, T value)
        {
            FieldInfo field = typeof(LevelConfig).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"[RFID Baggage] Could not find LevelConfig field: {fieldName}");
                return;
            }

            field.SetValue(level, value);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void CreateRenderTextureIfMissing(string assetPath)
        {
            RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
            if (renderTexture != null)
            {
                return;
            }

            renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                useMipMap = false,
                autoGenerateMips = false
            };

            AssetDatabase.CreateAsset(renderTexture, assetPath);
            EditorUtility.SetDirty(renderTexture);
        }
    }
}
