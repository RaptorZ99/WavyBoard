using UnityEditor;
namespace Biscotte.Tools
{
    public static class RefreshAssets
    {
        public static string Main()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "refresh done";
        }
    }
}
