namespace MHS2ModTool.CommonFileFormats
{
    enum ImageLoadResult
    {
        Success,
        CorruptedHeader,
        CorruptedData,
        DataTooShort,
        OutputTooShort,
        UnsupportedFormat,
    }
}
