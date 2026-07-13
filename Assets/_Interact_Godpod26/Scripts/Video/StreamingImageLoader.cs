using System;
using System.Collections;
using System.IO;
using RFIDBaggage.Utilities;
using UnityEngine;

namespace RFIDBaggage.Video
{
    public sealed class StreamingImageLoader : MonoBehaviour
    {
        private Coroutine loadCoroutine;
        private string currentRelativePath;

        public bool IsLoading { get; private set; }
        public Texture2D CurrentTexture { get; private set; }

        public void Load(string relativePath, Action<Texture2D> onCompleted, Action<string> onFailed)
        {
            if (string.Equals(currentRelativePath, relativePath, StringComparison.Ordinal) && CurrentTexture != null)
            {
                onCompleted?.Invoke(CurrentTexture);
                return;
            }

            if (loadCoroutine != null)
            {
                StopCoroutine(loadCoroutine);
                loadCoroutine = null;
            }

            loadCoroutine = StartCoroutine(LoadRoutine(relativePath, onCompleted, onFailed));
        }

        public void ReleaseCurrentTexture()
        {
            if (loadCoroutine != null)
            {
                StopCoroutine(loadCoroutine);
                loadCoroutine = null;
            }

            IsLoading = false;
            currentRelativePath = string.Empty;

            if (CurrentTexture != null)
            {
                Destroy(CurrentTexture);
                CurrentTexture = null;
            }
        }

        private IEnumerator LoadRoutine(string relativePath, Action<Texture2D> onCompleted, Action<string> onFailed)
        {
            IsLoading = true;

            if (!StreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath))
            {
                Fail($"Invalid image path: {relativePath}", onFailed);
                yield break;
            }

            if (!File.Exists(fullPath))
            {
                Fail($"Image file not found:\n{fullPath}", onFailed);
                yield break;
            }

            byte[] bytes = null;
            Exception readException = null;

            System.Threading.Tasks.Task readTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    bytes = File.ReadAllBytes(fullPath);
                }
                catch (Exception exception)
                {
                    readException = exception;
                }
            });

            while (!readTask.IsCompleted)
            {
                yield return null;
            }

            if (readException != null)
            {
                Fail(readException.Message, onFailed);
                yield break;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                Fail($"Unsupported image data:\n{fullPath}", onFailed);
                yield break;
            }

            DestroyCurrentTextureOnly();
            CurrentTexture = texture;
            currentRelativePath = relativePath;
            IsLoading = false;
            loadCoroutine = null;
            onCompleted?.Invoke(CurrentTexture);
        }

        private void Fail(string message, Action<string> onFailed)
        {
            IsLoading = false;
            loadCoroutine = null;
            Debug.LogWarning($"[Image] {message}", this);
            onFailed?.Invoke(message);
        }

        private void DestroyCurrentTextureOnly()
        {
            if (CurrentTexture != null)
            {
                Destroy(CurrentTexture);
                CurrentTexture = null;
            }
        }
    }
}
