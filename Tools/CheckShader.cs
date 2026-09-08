using UnityEditor;
using UnityEngine;

namespace Biscotte.Tools
{
    /// <summary>Imports the generated SurfWaveOcean shader graph and reports compile messages.
    /// Run: unity command run_script --file Tools/CheckShader.cs --entry Biscotte.Tools.CheckShader.Main --timeout_ms 300000 --timeout 400</summary>
    public static class CheckShader
    {
        const string kPath = "Assets/_Project/Shaders/SurfWaveOcean.shadergraph";

        public static string Main()
        {
            AssetDatabase.ImportAsset(kPath, ImportAssetOptions.ForceUpdate);
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(kPath);
            if (sh == null) return "shader null (import failed?)";
            var sb = new System.Text.StringBuilder();
            sb.Append(sh.name).Append(" supported=").Append(sh.isSupported).Append(" hasError=").Append(ShaderUtil.ShaderHasError(sh)).Append(" props=").Append(sh.GetPropertyCount());
            var msgs = ShaderUtil.GetShaderMessages(sh);
            foreach (var m in msgs) sb.Append("\n  ").Append(m.severity).Append(": ").Append(m.message).Append(" @").Append(m.line);
            // compile the shader for the editor platform to surface HLSL errors now rather than at first draw
            ShaderUtil.CompilePass(new Material(sh), 0, true);
            msgs = ShaderUtil.GetShaderMessages(sh);
            sb.Append("\nafter CompilePass: ").Append(msgs.Length).Append(" messages");
            foreach (var m in msgs) sb.Append("\n  ").Append(m.severity).Append(": ").Append(m.message).Append(" @").Append(m.line);
            return sb.ToString();
        }
    }
}
