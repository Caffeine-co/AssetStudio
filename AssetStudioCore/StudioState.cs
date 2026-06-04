using AssetStudio;
using CubismLive2DExtractor;
using System.Collections.Generic;

namespace AssetStudioCore.Runtime
{
    internal sealed class StudioState
    {
        public AssetsManager AssetsManager { get; } = new AssetsManager();
        public List<AssetItem> ParsedAssetsList { get; set; } = new List<AssetItem>();
        public List<BaseNode> GameObjectTree { get; } = new List<BaseNode>();
        public AssemblyLoader AssemblyLoader { get; } = new AssemblyLoader();
        public Dictionary<MonoBehaviour, CubismModel> Live2DModelDict { get; } = new Dictionary<MonoBehaviour, CubismModel>();
        public Dictionary<AssetStudio.Object, string> Containers { get; } = new Dictionary<AssetStudio.Object, string>();

        public void Clear()
        {
            AssetsManager.Clear();
            ParsedAssetsList.Clear();
            GameObjectTree.Clear();
            AssemblyLoader.Clear();
            Live2DModelDict.Clear();
            Containers.Clear();
        }
    }
}
