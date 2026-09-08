using System.Collections;
using System.IO;
using UnityEngine;

// Temporary dev helper: records deterministic 30 fps frames from a free camera to PNG files
// (Time.captureFramerate makes every rendered frame advance exactly 1/fps of game time).
// Not part of the game. Delete in P0 cleanup.
public class FrameDumper : MonoBehaviour
{
    public int fps = 30;
    public int totalFrames = 300;
    public int width = 1280;
    public int height = 720;
    public string folder = "Build/video_frames";
    public Vector3 startPosition = new Vector3(0f, 8f, -30f);
    public Vector3 startEuler = new Vector3(8f, 0f, 0f);
    public float moveSpeed = 1.2f;
    public float yawSpeed = 2.5f;
    public bool useExistingMainCamera = false;

    Camera cam;
    RenderTexture rt;
    Texture2D tex;
    int frame;
    bool done;
    bool started;

    void Start()
    {
        if (useExistingMainCamera && Camera.main != null)
        {
            cam = Camera.main;
        }
        else
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("VideoCamera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            go.transform.position = startPosition;
            go.transform.rotation = Quaternion.Euler(startEuler);
        }
        foreach (var mb in cam.GetComponents<MonoBehaviour>())
            if (mb != this && mb.GetType().Name == "CameraController") mb.enabled = false;

        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        Directory.CreateDirectory(folder);
        Time.captureFramerate = fps;
        started = true;
    }

    void Update()
    {
        if (!started || done || cam == null) return;
        var t = cam.transform;
        t.position += t.forward * (moveSpeed * Time.deltaTime);
        t.Rotate(0f, yawSpeed * Time.deltaTime, 0f, Space.World);
    }

    void LateUpdate()
    {
        if (!started || done || cam == null) return;
        StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();
        if (done) yield break;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(folder, $"frame_{frame:D4}.png"), tex.EncodeToPNG());
        frame++;
        if (frame >= totalFrames)
        {
            done = true;
            Time.captureFramerate = 0;
            cam.targetTexture = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
