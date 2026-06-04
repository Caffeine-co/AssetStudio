namespace AssetStudioCore.Options
{
    internal enum WorkMode
    {
        Extract,
        Export,
        ExportRaw,
        Dump,
        Info,
        Live2D,
        SplitObjects,
        Animator,
    }

    internal enum AssetGroupOption
    {
        None,
        TypeName,
        ContainerPath,
        ContainerPathFull,
        SourceFileName,
        SceneHierarchy,
    }

    internal enum FilenameFormat
    {
        AssetName,
        AssetName_PathID,
        PathID,
    }

    internal enum ExportListType
    {
        None,
        XML,
    }

    internal enum AudioFormat
    {
        None,
        Wav,
    }

    internal enum LogOutputMode
    {
        Console,
        File,
        Both,
    }

    internal enum FilterBy
    {
        None,
        Name,
        Container,
        PathID,
        NameOrContainer,
        NameAndContainer,
    }

    internal enum AnimationExportMode
    {
        Auto,
        Skip,
        All,
    }
}
