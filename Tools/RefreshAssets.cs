using UnityEditor;
namespace WavyBoard.Tools
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
