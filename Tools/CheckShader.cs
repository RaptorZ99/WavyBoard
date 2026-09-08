using UnityEditor;
using UnityEngine;

namespace Biscotte.Tools
{
    /// <summary>Imports the generated water shader graphs and reports compile messages.
    /// Run: unity command run_script --file Tools/CheckShader.cs --entry Biscotte.Tools.CheckShader.Main --timeout_ms 300000 --timeout 400</summary>
    public static class CheckShader
    {
        static readonly string[] kPaths =
        {
            "Assets/_Project/Shaders/SurfWaveOcean.shadergraph",
            "Assets/_Project/Shaders/OceanAmbientClip.shadergraph",
        };

        public static string Main()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var path in kPaths)
            {
                if (!System.IO.File.Exists(path)) { sb.Append(path).Append(": missing\n"); continue; }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) { sb.Append(path).Append(": shader null (import failed?)\n"); continue; }
                sb.Append(sh.name).Append(" supported=").Append(sh.isSupported).Append(" hasError=").Append(ShaderUtil.ShaderHasError(sh)).Append(" props=").Append(sh.GetPropertyCount());
                ShaderUtil.CompilePass(new Material(sh), 0, true);
                var msgs = ShaderUtil.GetShaderMessages(sh);
                sb.Append(" messages=").Append(msgs.Length);
                foreach (var m in msgs) sb.Append("\n  ").Append(m.severity).Append(": ").Append(m.message).Append(" @").Append(m.line);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
